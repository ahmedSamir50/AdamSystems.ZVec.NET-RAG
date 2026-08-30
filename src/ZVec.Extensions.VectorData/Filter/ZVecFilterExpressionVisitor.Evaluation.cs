using System.Linq.Expressions;
using System.Reflection;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;

namespace ZVec.Extensions.VectorData.Filter;

/// <summary>
/// AOT-safe expression evaluation helpers for <see cref="ZVecFilterExpressionVisitor"/>.
/// </summary>
public static partial class ZVecFilterExpressionVisitor
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

        if (method.Name is not (ZVecWellKnownMemberNames.OpImplicit or ZVecWellKnownMemberNames.OpExplicit))
            return false;

        if (AllowedConversionDeclaringTypes.Contains(method.DeclaringType))
            return true;

        return method.DeclaringType.Name.StartsWith(ZVecWellKnownMemberNames.ReadOnlySpanTypeNamePrefix, StringComparison.Ordinal);
    }

    private static void RejectUserDefinedConversionExpression(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryConvert &&
            IsUserDefinedConversionType(unaryConvert.Operand.Type) &&
            !IsUserDefinedConversionType(unaryConvert.Type))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    ZVecErrorMessages.UnsupportedUserDefinedConversion(unaryConvert.Operand.Type.Name, unaryConvert.Type.Name)),
                ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
        }

        expression = Unwrap(expression);

        if (expression is MethodCallExpression methodCall &&
            methodCall.Method.IsSpecialName &&
            methodCall.Method.Name is ZVecWellKnownMemberNames.OpImplicit or ZVecWellKnownMemberNames.OpExplicit &&
            !IsAllowedConversionOperator(methodCall.Method))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    ZVecErrorMessages.UnsupportedUserDefinedConversionOperator(
                        methodCall.Method.DeclaringType?.Name ?? ZVecWellKnownMemberNames.UnknownMember,
                        methodCall.Method.Name)),
                ZVecFilterErrorCode.UnsupportedUserDefinedConversion);
        }

        if (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary &&
            IsUserDefinedConversionType(unary.Operand.Type) &&
            !IsUserDefinedConversionType(unary.Type))
        {
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedFilterExpression(
                    ZVecErrorMessages.UnsupportedUserDefinedConversion(unary.Operand.Type.Name, unary.Type.Name)),
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
                    methodCallExpr.Method.Name is ZVecWellKnownMemberNames.OpImplicit or ZVecWellKnownMemberNames.OpExplicit)
                {
                    throw new ZVecFilterTranslationException(
                        ZVecErrorMessages.UnsupportedFilterExpression(
                            ZVecErrorMessages.UnsupportedUserDefinedConversionOperator(
                                methodCallExpr.Method.DeclaringType?.Name ?? ZVecWellKnownMemberNames.UnknownMember,
                                methodCallExpr.Method.Name)),
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
                    throw new ZVecFilterTranslationException(
                        ZVecErrorMessages.CannotEvaluateMethodUnderAot(methodCallExpr.Method.Name, ex.Message), ex);
                }

            case NewArrayExpression newArray:
                return CreateEvaluatedArray(newArray);
        }

        if (expression.Type.IsByRefLike)
            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(expression.Type.Name));

        throw new ZVecFilterTranslationException(
            ZVecErrorMessages.UnsupportedFilterExpression(
                ZVecErrorMessages.CannotStaticallyEvaluateExpressionUnderAot(expression.ToString())));
    }

    private static Array CreateEvaluatedArray(NewArrayExpression newArray)
    {
        Type? elementType = newArray.Type.GetElementType();
        if (elementType == typeof(string))
        {
            var values = new string[newArray.Expressions.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (string)Evaluate(newArray.Expressions[i])!;
            }

            return values;
        }

        if (elementType == typeof(int))
        {
            var values = new int[newArray.Expressions.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (int)Evaluate(newArray.Expressions[i])!;
            }

            return values;
        }

        if (elementType == typeof(long))
        {
            var values = new long[newArray.Expressions.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (long)Evaluate(newArray.Expressions[i])!;
            }

            return values;
        }

        if (elementType == typeof(Guid))
        {
            var values = new Guid[newArray.Expressions.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (Guid)Evaluate(newArray.Expressions[i])!;
            }

            return values;
        }

        if (elementType == typeof(DateTime))
        {
            var values = new DateTime[newArray.Expressions.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (DateTime)Evaluate(newArray.Expressions[i])!;
            }

            return values;
        }

        if (elementType == typeof(DateTimeOffset))
        {
            var values = new DateTimeOffset[newArray.Expressions.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (DateTimeOffset)Evaluate(newArray.Expressions[i])!;
            }

            return values;
        }

        throw new ZVecFilterTranslationException(
            ZVecErrorMessages.UnsupportedFilterExpression(
                ZVecErrorMessages.CannotStaticallyEvaluateExpressionUnderAot(newArray.Type.Name)));
    }
}
