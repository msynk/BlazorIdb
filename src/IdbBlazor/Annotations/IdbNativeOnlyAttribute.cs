namespace IdbBlazor.Annotations;

/// <summary>
/// When applied to an entity type or a query call,
/// requires that all predicates in a query be fully translated to native IndexedDB
/// operations.  If any predicate falls back to in-memory evaluation,
/// <see cref="IdbNativeQueryException"/> is thrown at query execution time.
/// </summary>
/// <remarks>
/// Apply to a class to enforce native-only queries for all queries against that store,
/// or call <see cref="IndexedDbQuery{T}.AsNativeOnly"/> on a per-query basis.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class IdbNativeOnlyAttribute : Attribute { }
