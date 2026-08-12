using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;
using ZVec.NET;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Translates LINQ filter expressions over vector records into native ZVec filter AST nodes and strings.
/// </summary>
/// <remarks>
/// <para>
/// <b>AST Translation Architecture:</b>
/// Maps C# expression trees into native ZVec boolean query AST format supporting all 12 primary operators:
/// Equal (<c>==</c>), NotEqual (<c>!=</c>), LessThan (<c>&lt;</c>), LessThanOrEqual (<c>&lt;=</c>),
/// GreaterThan (<c>&gt;</c>), GreaterThanOrEqual (<c>&gt;=</c>), AndAlso (<c>&amp;&amp;</c>), OrElse (<c>||</c>),
/// Not (<c>!</c>), ContainsAny (<c>x.Tags.Contains(value)</c> or <c>collection.Contains(x.Property)</c>),
/// IsNull, and IsNotNull.
/// </para>
/// <code>
/// ┌──────────────────────────────────────────────────────────────────────────────┐
/// │  Expression&lt;Func&lt;TRecord, bool&gt;&gt; LINQ AST                                    │
/// │  Example: x =&gt; x.Category == "Books" &amp;&amp; x.Price &lt; 100                        │
/// ├──────────────────────────────────────────────────────────────────────────────┤
/// │                                                                              │
/// │                    BinaryExpression (AndAlso)                                │
/// │                   /                      \                                   │
/// │    BinaryExpression (Equal)        BinaryExpression (LessThan)               │
/// │    /               \               /                 \                       │
/// │ MemberExpr      ConstantExpr    MemberExpr        ConstantExpr               │
/// │ (x.Category)    ("Books")       (x.Price)         (100)                      │
/// │                                                                              │
/// ├──────────────────────────────────────────────────────────────────────────────┤
/// │                     ZVecFilterExpressionVisitor                              │
/// │  1. VisitExpression → dispatches by ExpressionType                           │
/// │  2. VisitBinary    → AndAlso/OrElse/Relational ops                           │
/// │  3. VisitNot       → Logical negation                                        │
/// │  4. VisitMethodCall→ Contains patterns (IN / ContainAny)                     │
/// ├──────────────────────────────────────────────────────────────────────────────┤
/// │  ZVecFilterBuilder AST → Native ZVec SQL Filter String                       │
/// │  Output: (Category = "Books") AND (Price &lt; 100)                              │
/// └──────────────────────────────────────────────────────────────────────────────┘
/// </code>
/// </remarks>
public static class ZVecFilterExpressionVisitor
{
    private static readonly HashSet<Type> AllowedConversionDeclaringTypes = new()
    {
        typeof(decimal),
        typeof(double),
        typeof(float),
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(byte),
        typeof(uint),
        typeof(ulong),
        typeof(ushort),
        typeof(sbyte)
    };

    private static bool IsAllowedConversionOperator(MethodInfo method)
    {
        if (!method.IsSpecialName || method.DeclaringType == null)
            return false;

        if (method.Name is not ("op_Implicit" or "op_Explicit"))
            return false;

        if (AllowedConversionDeclaringTypes.Contains(method.DeclaringType))
            return true;

        return method.DeclaringType.Name.StartsWith("ReadOnlySpan", StringComparison.Ordinal);
    }
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
        return TranslateToBuilder(filter).Build();
    }

    /// <summary>
    /// Translates a typed predicate expression into a <see cref="ZVecFilterBuilder"/> AST instance.
    /// </summary>
    /// <typeparam name="TRecord">Record POCO type.</typeparam>
    /// <param name="filter">Boolean LINQ predicate expression.</param>
    /// <returns><see cref="ZVecFilterBuilder"/> AST node hierarchy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
    /// <exception cref="ZVecFilterTranslationException">Thrown when an unsupported LINQ expression shape is encountered.</exception>
    public static ZVecFilterBuilder TranslateToBuilder<TRecord>(Expression<Func<TRecord, bool>> filter) where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(filter);

        try
        {
            var model = ZVecTypeModel.Get<TRecord>();
            return VisitExpression(model, filter.Body);
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

    private static ZVecFilterBuilder VisitExpression(ZVecTypeModel model, Expression expression)
    {
        expression = Unwrap(expression);

        switch (expression)
        {
            case BinaryExpression binary:
                return VisitBinary(model, binary);

            case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                return VisitNot(model, unary);

            case MethodCallExpression methodCall:
                return VisitMethodCall(model, methodCall);

            case MemberExpression member when member.Type == typeof(bool):
                // Direct boolean property filter: x => x.InStock
                var propInfo = GetPropertyInfo(member) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(member.ToString()));
                var storageName = model.GetRequiredByPropertyName(propInfo.Name).StorageName;
                return ZVecFilterBuilder.Create().Where(storageName, ZVecCompareOp.Eq, true);

            default:
                throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(expression.ToString()));
        }
    }

    private static ZVecFilterBuilder VisitBinary(ZVecTypeModel model, BinaryExpression binary)
    {
        if (binary.NodeType == ExpressionType.AndAlso)
        {
            var left = VisitExpression(model, binary.Left);
            var right = VisitExpression(model, binary.Right);
            return left.And(right);
        }

        if (binary.NodeType == ExpressionType.OrElse)
        {
            var left = VisitExpression(model, binary.Left);
            var right = VisitExpression(model, binary.Right);
            return left.Or(right);
        }

        // Null comparison handling (x => x.Prop == null or x.Prop != null)
        if (IsNullCheck(binary, out var nullProp, out var isEquals))
        {
            var propInfoName = nullProp.Name;
            var storageName = model.GetRequiredByPropertyName(propInfoName).StorageName;
            return isEquals
                ? ZVecFilterBuilder.Create().IsNull(storageName)
                : ZVecFilterBuilder.Create().IsNotNull(storageName);
        }

        // Relational comparisons (==, !=, <, <=, >, >=)
        var (memberExpr, valueExpr) = ExtractMemberAndValue(binary.Left, binary.Right);
        if (memberExpr == null)
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(binary.ToString()));

        var prop = GetPropertyInfo(memberExpr) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(memberExpr.ToString()));
        var colName = model.GetRequiredByPropertyName(prop.Name).StorageName;
        RejectUserDefinedConversionExpression(valueExpr);
        var value = Evaluate(valueExpr);

        if (value != null && IsUserDefinedConversionType(value.GetType()))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    $"Value type '{value.GetType().Name}' is not supported in filter comparisons."),
                ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
        }

        var op = binary.NodeType switch
        {
            ExpressionType.Equal => ZVecCompareOp.Eq,
            ExpressionType.NotEqual => ZVecCompareOp.Ne,
            ExpressionType.LessThan => ZVecCompareOp.Lt,
            ExpressionType.LessThanOrEqual => ZVecCompareOp.Le,
            ExpressionType.GreaterThan => ZVecCompareOp.Gt,
            ExpressionType.GreaterThanOrEqual => ZVecCompareOp.Ge,
            _ => throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(binary.NodeType.ToString()))
        };

        var builder = ZVecFilterBuilder.Create();

        return value switch
        {
            int i => builder.Where(colName, op, i),
            long l => builder.Where(colName, op, l),
            float f => builder.Where(colName, op, f),
            double d => builder.Where(colName, op, d),
            bool b => builder.Where(colName, op, b),
            string s => builder.Where(colName, op, s),
            _ when value == null && op == ZVecCompareOp.Eq => builder.IsNull(colName),
            _ when value == null && op == ZVecCompareOp.Ne => builder.IsNotNull(colName),
            _ => builder.Where(colName, op, value?.ToString() ?? string.Empty)
        };
    }

    private static ZVecFilterBuilder VisitNot(ZVecTypeModel model, UnaryExpression unary)
    {
        var operand = Unwrap(unary.Operand);

        if (operand is MemberExpression member && member.Type == typeof(bool))
        {
            var propInfo = GetPropertyInfo(member) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(member.ToString()));
            var storageName = model.GetRequiredByPropertyName(propInfo.Name).StorageName;
            return ZVecFilterBuilder.Create().Where(storageName, ZVecCompareOp.Eq, false);
        }

        var innerBuilder = VisitExpression(model, operand);
        return ZVecFilterBuilder.Create().Not(innerBuilder);
    }

    private static ZVecFilterBuilder VisitMethodCall(ZVecTypeModel model, MethodCallExpression methodCall)
    {
        string methodName = methodCall.Method.Name;
        Type declaringType = methodCall.Method.DeclaringType ?? typeof(object);

        // Reject StartsWith, EndsWith, Regex.IsMatch with diagnostic remediation
        if (methodName == "StartsWith")
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedStartsWithMethod(GetMemberFieldName(methodCall.Object)),
                ZVecFilterErrorCode.UnsupportedStartsWith);

        if (methodName == "EndsWith")
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedEndsWithMethod(GetMemberFieldName(methodCall.Object)),
                ZVecFilterErrorCode.UnsupportedEndsWith);

        if (methodName == "IsMatch")
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedRegexMethod(GetMemberFieldName(methodCall.Arguments[0])),
                ZVecFilterErrorCode.UnsupportedRegex);

        // Reject string.Contains
        if (methodName == "Contains" && declaringType == typeof(string))
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedStringContainsMethod(GetMemberFieldName(methodCall.Object)),
                ZVecFilterErrorCode.UnsupportedStringContains);

        // Handle Enumerable.Contains / Collection.Contains
        if (methodName == nameof(Enumerable.Contains) || methodName == "Contains")
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
            {
                throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(methodName));
            }

            // Pattern: x.CollectionProperty.Contains(value) -> ContainAny
            if (TryGetRecordCollectionProperty(model, containerExpr, out var collectionStorageName))
            {
                RejectUserDefinedConversionExpression(itemExpr);
                var containValue = Evaluate(itemExpr);
                if (containValue == null)
                {
                    throw new ZVecFilterTranslationException(
                        ZVecErrorMessages.UnsupportedFilterExpression("ContainAny requires a non-null search value."));
                }

                return BuildContainAny(collectionStorageName, containValue);
            }

            // Pattern: externalCollection.Contains(x.ScalarProperty) -> IN
            var propInfo = GetPropertyInfo(itemExpr) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(itemExpr.ToString()));
            var property = model.GetRequiredByPropertyName(propInfo.Name);
            var rawValue = Evaluate(containerExpr);

            if (rawValue is IEnumerable enumerable and not string)
            {
                var nonNullValues = new List<object>();
                bool containsNull = false;

                foreach (var element in enumerable)
                {
                    if (element == null)
                    {
                        containsNull = true;
                    }
                    else
                    {
                        nonNullValues.Add(element);
                    }
                }

                var builder = ZVecFilterBuilder.Create();

                if (nonNullValues.Count > 0)
                {
                    var containBuilder = ZVecFilterBuilder.Create().In(property.StorageName, nonNullValues.ToArray());
                    builder = containsNull ? containBuilder.Or(b => b.IsNull(property.StorageName)) : containBuilder;
                }
                else if (containsNull)
                {
                    builder = ZVecFilterBuilder.Create().IsNull(property.StorageName);
                }
                else
                {
                    throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression("Empty IN clause collection."));
                }

                return builder;
            }

            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression("Invalid IN clause collection."));
        }

        throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(methodName));
    }

    private static (MemberExpression? member, Expression value) ExtractMemberAndValue(Expression left, Expression right)
    {
        left = Unwrap(left);
        right = Unwrap(right);

        if (left is MemberExpression leftMember && GetPropertyInfo(leftMember) != null)
            return (leftMember, right);

        if (right is MemberExpression rightMember && GetPropertyInfo(rightMember) != null)
            return (rightMember, left);

        return (null, right);
    }

    private static bool IsNullCheck(BinaryExpression binary, out PropertyInfo nullProp, out bool isEquals)
    {
        nullProp = null!;
        isEquals = binary.NodeType == ExpressionType.Equal;

        if (binary.NodeType != ExpressionType.Equal && binary.NodeType != ExpressionType.NotEqual)
            return false;

        var left = Unwrap(binary.Left);
        var right = Unwrap(binary.Right);

        if (left is ConstantExpression { Value: null } && right is MemberExpression rightMember)
        {
            var prop = GetPropertyInfo(rightMember);
            if (prop != null)
            {
                nullProp = prop;
                return true;
            }
        }

        if (right is ConstantExpression { Value: null } && left is MemberExpression leftMember)
        {
            var prop = GetPropertyInfo(leftMember);
            if (prop != null)
            {
                nullProp = prop;
                return true;
            }
        }

        return false;
    }

    private static PropertyInfo? GetPropertyInfo(Expression expression)
    {
        expression = Unwrap(expression);

        while (expression is MemberExpression member)
        {
            if (member.Member is PropertyInfo prop)
                return prop;

            expression = member.Expression ?? expression;
            if (expression is null or ParameterExpression)
                break;
        }

        return null;
    }

    private static bool TryGetRecordCollectionProperty(ZVecTypeModel model, Expression containerExpr, out string storageName)
    {
        storageName = null!;

        // Reject nested member access rooted at the record parameter (e.g. x.Order.Tags.Contains).
        // ContainAny only supports direct record properties. A chain like categoriesWrapper.Tags
        // (rooted at a closure constant, not the parameter) is the inverse IN pattern and must
        // fall through to the external-collection branch below — so only reject when the chain
        // bottoms out at the record ParameterExpression with more than one member level.
        var unwrapped = Unwrap(containerExpr);
        if (unwrapped is MemberExpression nestedMember &&
            nestedMember.Expression is MemberExpression &&
            IsRecordParameterRooted(nestedMember))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedNestedMemberAccess(containerExpr.ToString()),
                ZVecFilterErrorCode.UnsupportedExpression);
        }

        if (!IsRecordParameterMember(containerExpr))
            return false;

        var propInfo = GetPropertyInfo(containerExpr);
        if (propInfo == null || propInfo.PropertyType == typeof(string))
            return false;

        if (!typeof(IEnumerable).IsAssignableFrom(propInfo.PropertyType))
            return false;

        storageName = propInfo.Name;
        return true;
    }

    /// <summary>
    /// Walks a MemberExpression chain to determine whether it is rooted at the record
    /// parameter (e.g. x.Order.Tags) rather than a closure constant (e.g. wrapper.Tags).
    /// </summary>
    /// <param name="member">The outermost member expression to walk.</param>
    /// <returns><c>true</c> if the chain bottoms out at a <see cref="ParameterExpression"/>.</returns>
    private static bool IsRecordParameterRooted(MemberExpression member)
    {
        var current = member;
        while (current is not null)
        {
            if (current.Expression is ParameterExpression)
                return true;

            if (current.Expression is null || current.Expression is ConstantExpression)
                return false;

            current = current.Expression as MemberExpression;
        }

        return false;
    }

    private static bool IsRecordParameterMember(Expression expression)
    {
        expression = Unwrap(expression);

        while (expression is MemberExpression member)
        {
            if (member.Expression is ParameterExpression)
                return true;

            if (member.Expression is null)
                return false;

            expression = member.Expression;
        }

        return false;
    }

    private static ZVecFilterBuilder BuildContainAny(string storageName, object value)
    {
        return value switch
        {
            int i => ZVecFilterBuilder.Create().ContainAny(storageName, i),
            long l => ZVecFilterBuilder.Create().ContainAny(storageName, l),
            float f => ZVecFilterBuilder.Create().ContainAny(storageName, f),
            double d => ZVecFilterBuilder.Create().ContainAny(storageName, d),
            bool b => ZVecFilterBuilder.Create().ContainAny(storageName, b),
            string s => ZVecFilterBuilder.Create().ContainAny(storageName, s),
            Guid g => ZVecFilterBuilder.Create().ContainAny(storageName, g),
            DateTime dt => ZVecFilterBuilder.Create().ContainAny(storageName, dt),
            DateTimeOffset dto => ZVecFilterBuilder.Create().ContainAny(storageName, dto),
            _ => ZVecFilterBuilder.Create().ContainAny(storageName, value)
        };
    }

    private static string GetMemberFieldName(Expression? expression)
    {
        var propInfo = expression != null ? GetPropertyInfo(expression) : null;
        return propInfo?.Name ?? "unknown";
    }

    private static void RejectUserDefinedConversionExpression(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryConvert &&
            IsUserDefinedConversionType(unaryConvert.Operand.Type) &&
            !IsUserDefinedConversionType(unaryConvert.Type))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    $"User-defined conversion from '{unaryConvert.Operand.Type.Name}' to '{unaryConvert.Type.Name}' is not supported in filter expressions."),
                ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
        }

        expression = Unwrap(expression);

        if (expression is MethodCallExpression methodCall &&
            methodCall.Method.IsSpecialName &&
            methodCall.Method.Name is "op_Implicit" or "op_Explicit" &&
            !IsAllowedConversionOperator(methodCall.Method))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    $"User-defined conversion operator '{methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}' is not supported in filter expressions."),
                ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
        }

        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary &&
            IsUserDefinedConversionType(unary.Operand.Type) &&
            !IsUserDefinedConversionType(unary.Type))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    $"User-defined conversion from '{unary.Operand.Type.Name}' to '{unary.Type.Name}' is not supported in filter expressions."),
                ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
        }
    }

    private static bool IsUserDefinedConversionType(Type type) =>
        !type.IsPrimitive &&
        type != typeof(string) &&
        type != typeof(decimal) &&
        type != typeof(object) &&
        !type.IsEnum;

    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression unary &&
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            expression = unary.Operand;
        }

        while (expression is MethodCallExpression methodCall &&
               IsAllowedConversionOperator(methodCall.Method))
        {
            expression = methodCall.Arguments[0];
            expression = Unwrap(expression);
        }

        return expression;
    }

    /// <summary>
    /// Fully AOT-safe expression evaluator that avoids runtime Expression.Compile().DynamicInvoke().
    /// </summary>
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
                if (IsAllowedConversionOperator(methodCallExpr.Method))
                {
                    return Evaluate(methodCallExpr.Arguments[0]);
                }

                if (methodCallExpr.Method.IsSpecialName &&
                    methodCallExpr.Method.Name is "op_Implicit" or "op_Explicit")
                {
                    throw new ZVecFilterTranslationException(
                        ZVecErrorMessages.UnsupportedFilterExpression(
                            $"User-defined conversion operator '{methodCallExpr.Method.DeclaringType?.Name}.{methodCallExpr.Method.Name}' is not supported in filter expressions."),
                        ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
                }
                try
                {
                    object? objInstance = methodCallExpr.Object != null ? Evaluate(methodCallExpr.Object) : null;
                    var args = methodCallExpr.Arguments.Select(Evaluate).ToArray();
                    return methodCallExpr.Method.Invoke(objInstance, args);
                }
                catch (Exception ex)
                {
                    throw new ZVecFilterTranslationException($"Cannot evaluate method '{methodCallExpr.Method.Name}' under AOT: {ex.Message}", ex);
                }

            case NewArrayExpression newArray:
                var arrayElements = Array.CreateInstance(newArray.Type.GetElementType()!, newArray.Expressions.Count);
                for (int i = 0; i < newArray.Expressions.Count; i++)
                    arrayElements.SetValue(Evaluate(newArray.Expressions[i]), i);

                return arrayElements;
        }

        if (expression.Type.IsByRefLike)
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(expression.Type.Name));

        throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression($"Cannot statically evaluate expression '{expression}' under AOT without dynamic compilation."));
    }
}
