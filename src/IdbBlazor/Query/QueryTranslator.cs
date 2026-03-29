using System.Linq.Expressions;
using System.Reflection;
using IdbBlazor.Interop;
using IdbBlazor.Modeling;

namespace IdbBlazor.Query;

/// <summary>
/// Tries to translate a <see cref="Expression{TDelegate}"/> predicate into a
/// native IndexedDB <see cref="NativeQueryDescriptor"/>.
/// Translatable patterns:
/// <list type="bullet">
///   <item><c>x.Property == value</c> — index equality</item>
///   <item><c>x.Property &gt; value</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c> — index range</item>
///   <item><c>x.Property &gt; lb &amp;&amp; x.Property &lt; ub</c> — bounded range (same property)</item>
///   <item><c>x.Property.StartsWith("text")</c> — prefix range</item>
/// </list>
/// </summary>
public static class QueryTranslator
{
    /// <summary>
    /// Attempts to translate one predicate to a native query.
    /// Returns <c>null</c> if the predicate cannot be translated.
    /// </summary>
    public static NativeQueryDescriptor? TryTranslate<T>(
        Expression<Func<T, bool>> predicate,
        StoreDefinition store)
    {
        return TryTranslateExpression(predicate.Body, predicate.Parameters[0], store);
    }

    private static NativeQueryDescriptor? TryTranslateExpression(
        Expression body,
        ParameterExpression param,
        StoreDefinition store)
    {
        // Unwrap implicit convert
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        return body switch
        {
            BinaryExpression { NodeType: ExpressionType.Equal } eq
                => TryEquality(eq, param, store),

            BinaryExpression cmp when IsComparison(cmp.NodeType)
                => TryComparison(cmp, param, store),

            BinaryExpression { NodeType: ExpressionType.AndAlso } and
                => TryAndAlso(and, param, store),

            MethodCallExpression mc when mc.Method.Name == "StartsWith"
                => TryStartsWith(mc, param, store),

            _ => null
        };
    }

    // ---- Equality: x.Prop == value ----

    private static NativeQueryDescriptor? TryEquality(
        BinaryExpression eq, ParameterExpression param, StoreDefinition store)
    {
        var (propName, value) = ExtractPropertyValue(eq.Left, eq.Right, param);
        if (propName is null) return null;
        var indexName = FindIndex(propName, store);
        if (indexName is null) return null;

        return new NativeQueryDescriptor
        {
            IndexName = indexName == store.KeyPath ? null : indexName,
            Range = IdbKeyRange.Equality(value!)
        };
    }

    // ---- Comparison: >, >=, <, <= ----

    private static NativeQueryDescriptor? TryComparison(
        BinaryExpression cmp, ParameterExpression param, StoreDefinition store)
    {
        var (propName, value, flipped) = ExtractComparisonParts(cmp, param);
        if (propName is null) return null;
        var indexName = FindIndex(propName, store);
        if (indexName is null) return null;

        var op = flipped ? FlipOp(cmp.NodeType) : cmp.NodeType;
        var range = BuildSingleBound(op, value!);
        return range is null ? null : new NativeQueryDescriptor
        {
            IndexName = indexName == store.KeyPath ? null : indexName,
            Range = range
        };
    }

    // ---- AndAlso: try to merge two bounds on the same property ----

    private static NativeQueryDescriptor? TryAndAlso(
        BinaryExpression and, ParameterExpression param, StoreDefinition store)
    {
        var left = TryTranslateExpression(and.Left, param, store);
        var right = TryTranslateExpression(and.Right, param, store);

        if (left is null || right is null) return null;
        if (left.IndexName != right.IndexName) return null;

        // Both are on the same index — try to merge into a bounded range
        var merged = TryMergeBounds(left.Range, right.Range);
        return merged is null ? null : new NativeQueryDescriptor
        {
            IndexName = left.IndexName,
            Range = merged
        };
    }

    // ---- StartsWith: x.Prop.StartsWith("prefix") ----

    private static NativeQueryDescriptor? TryStartsWith(
        MethodCallExpression mc, ParameterExpression param, StoreDefinition store)
    {
        if (mc.Object is not MemberExpression { Member: PropertyInfo pi }) return null;
        if (!IsParamAccess(mc.Object, param)) return null;

        var prefixExpr = mc.Arguments.Count > 0 ? mc.Arguments[0] : null;
        if (prefixExpr is null) return null;

        var prefix = TryEvaluate(prefixExpr) as string;
        if (prefix is null) return null;

        var indexName = FindIndex(NamingHelper.ToCamelCase(pi.Name), store);
        if (indexName is null) return null;

        return new NativeQueryDescriptor
        {
            IndexName = indexName == store.KeyPath ? null : indexName,
            Range = IdbKeyRange.Bound(prefix, prefix + "\uffff")
        };
    }

    // ---- helpers ----

    private static (string? propName, object? value) ExtractPropertyValue(
        Expression left, Expression right, ParameterExpression param)
    {
        if (IsParamAccess(left, param) && left is MemberExpression { Member: PropertyInfo lpi })
        {
            var val = TryEvaluate(right);
            return (NamingHelper.ToCamelCase(lpi.Name), val);
        }
        if (IsParamAccess(right, param) && right is MemberExpression { Member: PropertyInfo rpi })
        {
            var val = TryEvaluate(left);
            return (NamingHelper.ToCamelCase(rpi.Name), val);
        }
        return (null, null);
    }

    private static (string? propName, object? value, bool flipped) ExtractComparisonParts(
        BinaryExpression cmp, ParameterExpression param)
    {
        if (IsParamAccess(cmp.Left, param) && cmp.Left is MemberExpression { Member: PropertyInfo lpi })
            return (NamingHelper.ToCamelCase(lpi.Name), TryEvaluate(cmp.Right), false);

        if (IsParamAccess(cmp.Right, param) && cmp.Right is MemberExpression { Member: PropertyInfo rpi })
            return (NamingHelper.ToCamelCase(rpi.Name), TryEvaluate(cmp.Left), true);

        return (null, null, false);
    }

    private static bool IsParamAccess(Expression expr, ParameterExpression param)
    {
        if (expr is UnaryExpression { NodeType: ExpressionType.Convert } u)
            expr = u.Operand;
        return expr is MemberExpression { Expression: ParameterExpression p } && p == param;
    }

    private static string? FindIndex(string camelCasePropName, StoreDefinition store)
    {
        // Primary key
        if (store.KeyPath == camelCasePropName) return camelCasePropName;
        // Regular index
        return store.Indexes.FirstOrDefault(i => i.KeyPath == camelCasePropName && !i.MultiEntry)?.Name;
    }

    private static IdbKeyRange? BuildSingleBound(ExpressionType op, object value) => op switch
    {
        ExpressionType.GreaterThan => IdbKeyRange.LowerBound(value, open: true),
        ExpressionType.GreaterThanOrEqual => IdbKeyRange.LowerBound(value, open: false),
        ExpressionType.LessThan => IdbKeyRange.UpperBound(value, open: true),
        ExpressionType.LessThanOrEqual => IdbKeyRange.UpperBound(value, open: false),
        _ => null
    };

    private static IdbKeyRange? TryMergeBounds(IdbKeyRange? a, IdbKeyRange? b)
    {
        if (a is null || b is null) return null;
        // Both must be single-sided bounds
        object? lower = a.Lower ?? b.Lower;
        object? upper = a.Upper ?? b.Upper;
        bool lowerOpen = a.Lower != null ? a.LowerOpen : b.LowerOpen;
        bool upperOpen = a.Upper != null ? a.UpperOpen : b.UpperOpen;

        if (lower is null || upper is null) return null;
        return IdbKeyRange.Bound(lower, upper, lowerOpen, upperOpen);
    }

    private static bool IsComparison(ExpressionType t) =>
        t is ExpressionType.GreaterThan
            or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LessThan
            or ExpressionType.LessThanOrEqual;

    private static ExpressionType FlipOp(ExpressionType t) => t switch
    {
        ExpressionType.GreaterThan => ExpressionType.LessThan,
        ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
        ExpressionType.LessThan => ExpressionType.GreaterThan,
        ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
        _ => t
    };

    /// <summary>Evaluates a constant or closure-variable expression.</summary>
    private static object? TryEvaluate(Expression expr)
    {
        try
        {
            return Expression.Lambda(expr).Compile().DynamicInvoke();
        }
        catch
        {
            return null;
        }
    }
}
