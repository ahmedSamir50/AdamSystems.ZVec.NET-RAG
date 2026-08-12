using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Translates LINQ filter expressions over vector records into native ZVec filter strings.
/// </summary>
/// <remarks>
/// <para>
/// <b>AST Translation Architecture:</b>
/// Maps C# expression trees into native ZVec boolean query AST format supporting 10 primary operators:
/// Equal (<c>==</c>), NotEqual (<c>!=</c>), LessThan (<c>&lt;</c>), LessThanOrEqual (<c>&lt;=</c>),
/// GreaterThan (<c>&gt;</c>), GreaterThanOrEqual (<c>&gt;=</c>), AndAlso (<c>&amp;&amp;</c>), OrElse (<c>||</c>),
/// Not (<c>!</c>), and ContainsAny (<c>Enumerable.Contains</c>).
/// </para>
/// <code>
/// ┌─────────────────────────────────────────────────────────────┐
/// │         Expression&lt;Func&lt;TRecord, bool&gt;&gt; LINQ AST            │
/// ├─────────────────────────────────────────────────────────────┤
/// │               ZVecFilterExpressionVisitor                   │
/// ├─────────────────────────────────────────────────────────────┤
/// │      ZVecFilterBuilder ──► Native ZVec SQL Filter String    │
/// └─────────────────────────────────────────────────────────────┘
/// </code>
/// </remarks>
public static class ZVecFilterExpressionVisitor
{
    /// <summary>
    /// Translates a typed predicate expression into a native ZVec SQL filter string.
    /// </summary>
    /// <typeparam name="TRecord">Record POCO type.</typeparam>
    /// <param name="filter">Boolean LINQ predicate expression.</param>
    /// <returns>Native ZVec filter string representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
    /// <exception cref="ZVecFilterTranslationException">Thrown when an unsupported LINQ expression shape is encountered.</exception>
    public static string Translate<TRecord>(Expression<Func<TRecord, bool>> filter) where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(filter);

        try
        {
            var model = ZVecTypeModel.Get<TRecord>();
            return TranslateCore<TRecord>(model, filter.Body, filter.Parameters[0]);
        }
        catch (ZVecFilterTranslationException)
        {
            throw;
        }
        catch (ZVecException ex)
        {
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(ex.Message), ex);
        }
        catch (Exception ex)
        {
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(ex.Message), ex);
        }
    }

    /// <summary>
    /// Core translation engine that dispatches expression body nodes to the appropriate handler.
    /// </summary>
    /// <typeparam name="TRecord">Record POCO type.</typeparam>
    /// <param name="model">ZVec type model for the record.</param>
    /// <param name="body">The body expression to translate.</param>
    /// <param name="parameter">The original parameter expression from the user's lambda (preserves reference equality).</param>
    /// <returns>Native ZVec filter string.</returns>
    private static string TranslateCore<TRecord>(ZVecTypeModel model, Expression body, ParameterExpression parameter) where TRecord : class
    {
        body = Unwrap(body);

        switch (body)
        {
            case MethodCallExpression methodCall:
                return VisitMethodCall(model, methodCall);

            case UnaryExpression { NodeType: ExpressionType.Not } unary when Unwrap(unary.Operand) is MemberExpression member:
                // Rewrite !x.Property -> x.Property == false
                // Preserves the original parameter reference to avoid reference-equality mismatch.
                var eqFalse = Expression.Equal(member, Expression.Constant(false));
                return ZVecExpressionFilter.Translate(model, Expression.Lambda<Func<TRecord, bool>>(eqFalse, parameter));

            default:
                var lambda = Expression.Lambda<Func<TRecord, bool>>(body, parameter);
                return ZVecExpressionFilter.Translate(model, lambda);
        }
    }

    private static string VisitMethodCall(ZVecTypeModel model, MethodCallExpression methodCall)
    {
        if (methodCall.Method.Name == nameof(Enumerable.Contains))
        {
            Expression containerExpr;
            Expression itemExpr;

            if (methodCall.Object != null)
            {
                containerExpr = methodCall.Object;
                itemExpr = methodCall.Arguments[0];
            }
            else if (methodCall.Arguments.Count >= 2)
            {
                containerExpr = methodCall.Arguments[0];
                itemExpr = methodCall.Arguments[1];
            }
            else
                throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(methodCall.Method.Name));

            var propInfo = GetPropertyInfo(itemExpr) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(itemExpr.ToString()));
            var property = model.GetRequiredByPropertyName(propInfo.Name);
            var rawValue = Evaluate(containerExpr);

            if (rawValue is IEnumerable enumerable)
            {
                var elements = new List<string>();
                foreach (var element in enumerable)
                {
                    if (element == null) continue;
                    elements.Add($"\"{element}\"");
                }

                if (elements.Count > 0)
                    return $"{property.StorageName} IN ({string.Join(", ", elements)})";
            }

            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression("Empty or invalid IN clause collection."));
        }

        throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(methodCall.Method.Name));
    }

    private static PropertyInfo? GetPropertyInfo(Expression expression)
    {
        expression = Unwrap(expression);
        if (expression is MemberExpression member && member.Member is PropertyInfo prop)
            return prop;

        return null;
    }

    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression unary &&
               (unary.NodeType == ExpressionType.Convert || 
                unary.NodeType == ExpressionType.ConvertChecked)
               )
        {
            expression = unary.Operand;
        }
        return expression;
    }

    private static object? Evaluate(Expression expression)
    {
        expression = Unwrap(expression);

        switch (expression)
        {
            case ConstantExpression constant:
                return constant.Value;

            case MemberExpression memberExpr:
                object? instance = memberExpr.Expression != null ? Evaluate(memberExpr.Expression) : null;
                if (memberExpr.Member is FieldInfo fieldInfo)
                    return fieldInfo.GetValue(instance);
                
                if (memberExpr.Member is PropertyInfo propInfo)
                    return propInfo.GetValue(instance, null);
                break;

            case MethodCallExpression methodCallExpr:
                if (methodCallExpr.Object != null)
                    return Evaluate(methodCallExpr.Object);
                
                if (methodCallExpr.Arguments.Count > 0)
                    return Evaluate(methodCallExpr.Arguments[0]);
                break;

            case NewArrayExpression newArray:
                var arrayElements = Array.CreateInstance(newArray.Type.GetElementType()!, newArray.Expressions.Count);
                for (int i = 0; i < newArray.Expressions.Count; i++)
                    arrayElements.SetValue(Evaluate(newArray.Expressions[i]), i);
              
                return arrayElements;
        }

        if (expression.Type.IsByRefLike)
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(expression.Type.Name));


        var delegateType = typeof(Func<>).MakeGenericType(expression.Type);
        var lambda = Expression.Lambda(delegateType, expression);
        return lambda.Compile().DynamicInvoke();
    }
}
