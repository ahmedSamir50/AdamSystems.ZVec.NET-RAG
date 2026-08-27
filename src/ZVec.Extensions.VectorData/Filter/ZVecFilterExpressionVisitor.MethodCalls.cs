using System.Collections;
using System.Linq.Expressions;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData.Filter;

/// <summary>
/// Method-call translation helpers for <see cref="ZVecFilterExpressionVisitor"/>.
/// </summary>
public static partial class ZVecFilterExpressionVisitor
{
    private static ZVecFilterBuilder VisitMethodCall(ZVecFilterRecordModel model, MethodCallExpression methodCall)
    {
        string methodName = methodCall.Method.Name;
        Type declaringType = methodCall.Method.DeclaringType ?? typeof(object);

        // Reject StartsWith, EndsWith, Regex.IsMatch with diagnostic remediation
        if (methodName == ZVecWellKnownMemberNames.StartsWith)
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedStartsWithMethod(GetMemberFieldName(methodCall.Object)),
                ZVecFilterErrorCode.UnsupportedStartsWith);

        if (methodName == ZVecWellKnownMemberNames.EndsWith)
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedEndsWithMethod(GetMemberFieldName(methodCall.Object)),
                ZVecFilterErrorCode.UnsupportedEndsWith);

        if (methodName == ZVecWellKnownMemberNames.IsMatch)
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedRegexMethod(GetMemberFieldName(methodCall.Arguments[0])),
                ZVecFilterErrorCode.UnsupportedRegex);

        // Reject string.Contains
        if (methodName == ZVecWellKnownMemberNames.Contains && declaringType == typeof(string))
            throw new ZVecFilterTranslationException(
                ZVecErrorMessages.UnsupportedStringContainsMethod(GetMemberFieldName(methodCall.Object)),
                ZVecFilterErrorCode.UnsupportedStringContains);

        // Handle Enumerable.Contains / Collection.Contains
        if (methodName == nameof(Enumerable.Contains) || methodName == ZVecWellKnownMemberNames.Contains)
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
                        ZVecErrorMessages.UnsupportedFilterExpression(ZVecErrorMessages.ContainAnyRequiresNonNullValue));
                }

                return BuildContainAny(collectionStorageName, containValue);
            }

            // Pattern: externalCollection.Contains(x.ScalarProperty) -> IN
            var propInfo = GetPropertyInfo(itemExpr) ?? throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(itemExpr.ToString()));
            var storageName = model.GetStorageName(propInfo.Name);
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
                    var containBuilder = ZVecFilterBuilder.Create().In(storageName, nonNullValues.ToArray());
                    builder = containsNull ? containBuilder.Or(b => b.IsNull(storageName)) : containBuilder;
                }
                else if (containsNull)
                {
                    builder = ZVecFilterBuilder.Create().IsNull(storageName);
                }
                else
                {
                    throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(ZVecErrorMessages.EmptyInClauseCollection));
                }

                return builder;
            }

            throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(ZVecErrorMessages.InvalidInClauseCollection));
        }

        throw new ZVecFilterTranslationException(ZVecErrorMessages.UnsupportedFilterExpression(methodName));
    }

    private static bool TryGetRecordCollectionProperty(ZVecFilterRecordModel model, Expression containerExpr, out string storageName)
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
}
