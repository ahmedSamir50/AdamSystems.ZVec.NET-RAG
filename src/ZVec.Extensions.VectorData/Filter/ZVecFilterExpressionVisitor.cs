using System.Linq.Expressions;
using System.Reflection;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;
using ZVec.Extensions.VectorData.Mapping;
using ZVec.NET;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData.Filter;

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
public static partial class ZVecFilterExpressionVisitor
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
            var model = ZVecFilterRecordModel.Resolve<TRecord>();
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

    private static ZVecFilterBuilder VisitExpression(ZVecFilterRecordModel model, Expression expression)
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
                var storageName = model.GetStorageName(propInfo.Name);
                return ZVecFilterBuilder.Create().Where(storageName, ZVecCompareOp.Eq, true);

            default:
                throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(expression.ToString()));
        }
    }

    private static ZVecFilterBuilder VisitBinary(ZVecFilterRecordModel model, BinaryExpression binary)
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
            var storageName = model.GetStorageName(propInfoName);
            return isEquals
                ? ZVecFilterBuilder.Create().IsNull(storageName)
                : ZVecFilterBuilder.Create().IsNotNull(storageName);
        }

        // Relational comparisons (==, !=, <, <=, >, >=)
        var (memberExpr, valueExpr) = ExtractMemberAndValue(binary.Left, binary.Right);
        if (memberExpr == null)
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(binary.ToString()));

        var prop = GetPropertyInfo(memberExpr) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(memberExpr.ToString()));
        var colName = model.GetStorageName(prop.Name);
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

    private static ZVecFilterBuilder VisitNot(ZVecFilterRecordModel model, UnaryExpression unary)
    {
        var operand = Unwrap(unary.Operand);

        if (operand is MemberExpression member && member.Type == typeof(bool))
        {
            var propInfo = GetPropertyInfo(member) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(member.ToString()));
            var storageName = model.GetStorageName(propInfo.Name);
            return ZVecFilterBuilder.Create().Where(storageName, ZVecCompareOp.Eq, false);
        }

        var innerBuilder = VisitExpression(model, operand);
        return ZVecFilterBuilder.Create().Not(innerBuilder);
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

    private static string GetMemberFieldName(Expression? expression)
    {
        var propInfo = expression != null ? GetPropertyInfo(expression) : null;
        return propInfo?.Name ?? ZVecWellKnownMemberNames.UnknownMember;
    }
}
