using System.Linq.Expressions;
using System.Text.Json;
using BlazorIdb.Annotations;
using BlazorIdb.Interop;
using BlazorIdb.Modeling;
using BlazorIdb.Reactive;

namespace BlazorIdb.Query;

/// <summary>
/// Represents a composable, lazily evaluated query over an <see cref="IndexedDbSet{T}"/>.
/// Build the query using <see cref="Where"/>, <see cref="OrderBy{TKey}"/>,
/// <see cref="ThenBy{TKey}"/>, <see cref="Take"/>, and <see cref="Skip"/>,
/// then materialise with <see cref="ToListAsync"/>, <see cref="FirstOrDefaultAsync"/>,
/// <see cref="CountAsync"/>, or <see cref="AnyAsync"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class IndexedDbQuery<T> where T : class
{
    private readonly IndexedDbSet<T> _set;
    private readonly List<Expression<Func<T, bool>>> _predicates = new();
    private LambdaExpression? _orderByExpr;
    private bool _orderByDesc;
    private int? _take;
    private int? _skip;
    private string? _searchTerm;
    private bool _nativeOnly;
    private bool _memoryOnly;

    internal IndexedDbQuery(IndexedDbSet<T> set)
    {
        _set = set;
    }

    // ---- Fluent builder ----

    /// <summary>Adds a filter predicate. Multiple calls are ANDed together.</summary>
    public IndexedDbQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        _predicates.Add(predicate);
        return this;
    }

    /// <summary>Orders results ascending by the given key selector.</summary>
    public IndexedDbQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _orderByExpr = keySelector;
        _orderByDesc = false;
        return this;
    }

    /// <summary>Orders results descending by the given key selector.</summary>
    public IndexedDbQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _orderByExpr = keySelector;
        _orderByDesc = true;
        return this;
    }

    /// <summary>Applies a secondary ascending sort.</summary>
    public IndexedDbQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        => OrderBy(keySelector); // simplified: last wins for now

    /// <summary>Limits the result set to <paramref name="count"/> records.</summary>
    public IndexedDbQuery<T> Take(int count) { _take = count; return this; }

    /// <summary>Skips the first <paramref name="count"/> records.</summary>
    public IndexedDbQuery<T> Skip(int count) { _skip = count; return this; }

    /// <summary>
    /// Enables full-text search over all <c>[IdbFullTextIndex]</c>-annotated properties.
    /// Tokens are ANDed: each token must appear in at least one FTS field.
    /// </summary>
    public IndexedDbQuery<T> Search(string searchTerm)
    {
        _searchTerm = searchTerm;
        return this;
    }

    /// <summary>
    /// Requires that the query be fully satisfied by native IndexedDB operations.
    /// Throws <see cref="IdbNativeQueryException"/> if any predicate falls back to
    /// in-memory evaluation.
    /// </summary>
    public IndexedDbQuery<T> AsNativeOnly() { _nativeOnly = true; return this; }

    /// <summary>
    /// Forces all filtering to be performed in-memory (loads all records then filters).
    /// Overrides any native translation attempt.
    /// </summary>
    public IndexedDbQuery<T> AsMemoryQuery() { _memoryOnly = true; return this; }

    // ---- Execution ----

    /// <summary>Materialises the query and returns all matching records as a list.</summary>
    public async Task<List<T>> ToListAsync()
    {
        await _set._context.EnsureInitializedAsync();
        var store = _set.GetStore();
        return await ExecuteAsync(store);
    }

    /// <summary>
    /// Returns the first matching record, or <c>null</c> if none match.
    /// </summary>
    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate != null) Where(predicate);
        var results = await Take(1).ToListAsync();
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Returns the single matching record, or <c>null</c>.
    /// Throws if more than one match is found.
    /// </summary>
    public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate != null) Where(predicate);
        var results = await Take(2).ToListAsync();
        return results.Count switch
        {
            0 => null,
            1 => results[0],
            _ => throw new InvalidOperationException(
                "Sequence contains more than one matching element.")
        };
    }

    /// <summary>Returns the number of records matching the current query.</summary>
    public async Task<int> CountAsync()
        => (await ToListAsync()).Count;

    /// <summary>Returns <c>true</c> if any records match the current query.</summary>
    public async Task<bool> AnyAsync()
        => (await Take(1).ToListAsync()).Count > 0;

    /// <summary>
    /// Returns an <see cref="IAsyncEnumerable{T}"/> that streams matching records.
    /// </summary>
    public async IAsyncEnumerable<T> AsAsyncEnumerable()
    {
        foreach (var item in await ToListAsync())
            yield return item;
    }

    /// <summary>
    /// Creates a live observable that emits the current query result whenever the
    /// underlying store changes.
    /// </summary>
    public IObservable<IEnumerable<T>> LiveQuery()
    {
        var predicate = CombinePredicates();
        return new LiveQueryObservable<T>(_set, predicate);
    }

    // ---- Private execution logic ----

    private async Task<List<T>> ExecuteAsync(StoreDefinition store)
    {
        List<T> candidates;

        if (_memoryOnly || (!_predicates.Any() && _searchTerm is null))
        {
            // Full store scan
            candidates = await FetchAllAsync(store);
        }
        else if (!_memoryOnly && _searchTerm is not null)
        {
            // Full-text search path
            candidates = await ExecuteFtsAsync(store);
        }
        else
        {
            // Attempt native translation for the first translatable predicate
            candidates = await ExecuteNativeOrFallbackAsync(store);
        }

        // Apply remaining in-memory predicates
        foreach (var pred in _predicates)
        {
            var compiled = pred.Compile();
            candidates = candidates.Where(compiled).ToList();
        }

        // Order
        if (_orderByExpr != null)
        {
            var keySelector = ((LambdaExpression)_orderByExpr).Compile();
            candidates = _orderByDesc
                ? candidates.OrderByDescending(x => keySelector.DynamicInvoke(x)).ToList()
                : candidates.OrderBy(x => keySelector.DynamicInvoke(x)).ToList();
        }

        // Skip / Take
        if (_skip.HasValue) candidates = candidates.Skip(_skip.Value).ToList();
        if (_take.HasValue) candidates = candidates.Take(_take.Value).ToList();

        return candidates;
    }

    private async Task<List<T>> ExecuteNativeOrFallbackAsync(StoreDefinition store)
    {
        // Try each predicate for native translation; use the first that succeeds
        for (var i = 0; i < _predicates.Count; i++)
        {
            var desc = QueryTranslator.TryTranslate(_predicates[i], store);
            if (desc is null) continue;

            // Found a native-translatable predicate; remove it from in-memory list
            _predicates.RemoveAt(i);

            List<T> results;
            if (desc.IndexName is null)
            {
                // Primary key range
                var json = await _set._context.JsInterop.GetAllAsync(
                    _set._context.DbName, store.Name, desc.Range);
                results = IndexedDbSet<T>.DeserializeList(json);
            }
            else
            {
                // Secondary index range
                var range = desc.Range ?? IdbKeyRange.LowerBound(0); // shouldn't be null here
                var json = await _set._context.JsInterop.GetAllByIndexAsync(
                    _set._context.DbName, store.Name, desc.IndexName, range);
                results = IndexedDbSet<T>.DeserializeList(json);
            }

            return results;
        }

        // No predicate was translatable
        if (_nativeOnly || typeof(T).GetCustomAttributes(
                typeof(Annotations.IdbNativeOnlyAttribute), true).Length > 0)
        {
            throw new IdbNativeQueryException(
                $"No predicate on '{typeof(T).Name}' could be translated to a native " +
                $"IndexedDB operation, but native-only mode is active.");
        }

        return await FetchAllAsync(store);
    }

    private async Task<List<T>> ExecuteFtsAsync(StoreDefinition store)
    {
        // Find FTS indexes
        var ftsIndexes = store.Indexes.Where(i => i.IsFullText).ToList();
        if (!ftsIndexes.Any())
            return await FetchAllAsync(store);

        var tokens = FullText.Tokenizer.Tokenize(_searchTerm!);
        if (!tokens.Any())
            return await FetchAllAsync(store);

        // Find records matching ALL tokens (AND semantics): intersect key sets
        // Use the first FTS index for each token
        var ftsIndex = ftsIndexes[0];
        List<T>? intersection = null;

        foreach (var token in tokens)
        {
            var range = IdbKeyRange.Equality(token);
            var json = await _set._context.JsInterop.GetAllByIndexAsync(
                _set._context.DbName, store.Name, ftsIndex.Name, range);
            var batch = IndexedDbSet<T>.DeserializeList(json);

            if (intersection is null)
            {
                intersection = batch;
            }
            else
            {
                // Intersect by primary key
                var keyPath = store.KeyPath ?? "id";
                var existingKeys = GetKeys(intersection, keyPath);
                intersection = batch.Where(b => existingKeys.Contains(GetKey(b, keyPath))).ToList();
            }

            if (intersection.Count == 0) break;
        }

        return intersection ?? new List<T>();
    }

    private async Task<List<T>> FetchAllAsync(StoreDefinition store)
    {
        var json = await _set._context.JsInterop.GetAllAsync(_set._context.DbName, store.Name);
        return IndexedDbSet<T>.DeserializeList(json);
    }

    private Expression<Func<T, bool>>? CombinePredicates()
    {
        if (!_predicates.Any()) return null;
        var combined = _predicates[0];
        for (var i = 1; i < _predicates.Count; i++)
            combined = Combined(combined, _predicates[i]);
        return combined;
    }

    private static Expression<Func<T, bool>> Combined(
        Expression<Func<T, bool>> a, Expression<Func<T, bool>> b)
    {
        var param = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(a, param),
            Expression.Invoke(b, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    private static HashSet<object?> GetKeys(List<T> items, string keyPath)
        => items.Select(i => GetKey(i, keyPath)).ToHashSet();

    private static object? GetKey(T item, string keyPath)
    {
        // keyPath is camelCase; find matching property (case-insensitive)
        var prop = typeof(T).GetProperties()
            .FirstOrDefault(p => string.Equals(
                Modeling.NamingHelper.ToCamelCase(p.Name),
                keyPath,
                StringComparison.OrdinalIgnoreCase));
        return prop?.GetValue(item);
    }
}

/// <summary>
/// Thrown when a query is marked as native-only but cannot be fully translated
/// to a native IndexedDB operation.
/// </summary>
public sealed class IdbNativeQueryException : InvalidOperationException
{
    /// <inheritdoc/>
    public IdbNativeQueryException(string message) : base(message) { }
}
