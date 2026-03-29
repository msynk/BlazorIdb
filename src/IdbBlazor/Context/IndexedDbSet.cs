using System.Text.Json;
using IdbBlazor.FullText;
using IdbBlazor.Interop;
using IdbBlazor.Modeling;
using IdbBlazor.Query;

namespace IdbBlazor;

/// <summary>
/// Represents an IndexedDB object store scoped to entity type <typeparamref name="T"/>.
/// Provides CRUD operations, LINQ-style queries, full-text search, and live queries.
/// Obtain instances from <see cref="IndexedDbContext.Set{T}"/> or via
/// <see cref="IndexedDbContext"/> property declarations.
/// </summary>
/// <typeparam name="T">The CLR entity type stored in this object store.</typeparam>
public sealed class IndexedDbSet<T> where T : class
{
    internal readonly IndexedDbContext _context;

    internal IndexedDbSet(IndexedDbContext context)
    {
        _context = context;
    }

    // ---- CRUD ----

    /// <summary>
    /// Adds a new record to the store.
    /// If a record with the same key already exists, the operation throws.
    /// </summary>
    public async Task AddAsync(T entity)
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        FullTextIndexMaintainer.Maintain(entity, store);
        var json = Serialize(entity);
        await _context.JsInterop.AddAsync(_context.DbName, store.Name, json);
    }

    /// <summary>
    /// Updates an existing record (or inserts it if the key does not exist).
    /// Equivalent to IndexedDB's <c>put()</c>.
    /// </summary>
    public async Task UpdateAsync(T entity)
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        FullTextIndexMaintainer.Maintain(entity, store);
        var json = Serialize(entity);
        await _context.JsInterop.PutAsync(_context.DbName, store.Name, json);
    }

    /// <summary>
    /// Inserts or replaces a record. Equivalent to <see cref="UpdateAsync"/>.
    /// </summary>
    public Task PutAsync(T entity) => UpdateAsync(entity);

    /// <summary>Deletes a record by its primary key.</summary>
    public async Task DeleteAsync(object key)
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        await _context.JsInterop.DeleteAsync(_context.DbName, store.Name, JsonSerializer.Serialize(key));
    }

    /// <summary>
    /// Finds and returns a single record by primary key, or <c>null</c> if not found.
    /// </summary>
    public async Task<T?> FindAsync(object key)
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        var keyJson = JsonSerializer.Serialize(key);
        var json = await _context.JsInterop.GetAsync(_context.DbName, store.Name, keyJson);
        return json is null ? null : Deserialize(json);
    }

    // ---- Bulk operations ----

    /// <summary>Returns all records from the store as a list.</summary>
    public async Task<List<T>> ToListAsync()
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        var json = await _context.JsInterop.GetAllAsync(_context.DbName, store.Name);
        return DeserializeList(json);
    }

    /// <summary>Removes all records from the store.</summary>
    public async Task ClearAsync()
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        await _context.JsInterop.ClearAsync(_context.DbName, store.Name);
    }

    /// <summary>Returns the total number of records in the store.</summary>
    public async Task<int> CountAsync()
    {
        await _context.EnsureInitializedAsync();
        var store = GetStore();
        return await _context.JsInterop.CountAsync(_context.DbName, store.Name);
    }

    // ---- LINQ query entry point ----

    /// <summary>
    /// Starts a composable query over this store.
    /// Returns an <see cref="IndexedDbQuery{T}"/> that can be further refined
    /// with <c>Where</c>, <c>OrderBy</c>, <c>Take</c>, and <c>Skip</c> before
    /// executing via <c>ToListAsync()</c> or similar.
    /// </summary>
    public IndexedDbQuery<T> AsQueryable() => new(this);

    /// <summary>
    /// Adds a <c>Where</c> predicate to a new query over this store.
    /// Shorthand for <c>AsQueryable().Where(predicate)</c>.
    /// </summary>
    public IndexedDbQuery<T> Where(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        => new IndexedDbQuery<T>(this).Where(predicate);

    /// <summary>
    /// Performs a full-text search across all <c>[IdbFullTextIndex]</c>-decorated
    /// properties on <typeparamref name="T"/>.
    /// Multiple terms are ANDed together by default.
    /// </summary>
    /// <param name="searchTerm">
    /// One or more whitespace-separated search tokens.
    /// </param>
    public IndexedDbQuery<T> Search(string searchTerm)
        => new IndexedDbQuery<T>(this).Search(searchTerm);

    /// <summary>
    /// Creates a live query that emits the current result set whenever the store
    /// changes (using poll-based observation).
    /// Dispose the returned <see cref="IDisposable"/> to cancel.
    /// </summary>
    public Reactive.LiveQueryObservable<T> LiveQuery(
        System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null)
    {
        return new Reactive.LiveQueryObservable<T>(this, predicate);
    }

    // ---- Internal helpers ----

    internal StoreDefinition GetStore()
        => _context.Model.GetStore(typeof(T));

    internal static string Serialize(T entity)
        => JsonSerializer.Serialize(entity, IndexedDbContext.JsonOptions);

    /// <summary>Deserializes a JSON string to <typeparamref name="T"/>.</summary>
    public static T Deserialize(string json)
        => JsonSerializer.Deserialize<T>(json, IndexedDbContext.JsonOptions)!;

    // Keep a public alias for tests
    /// <summary>Serializes an entity to JSON using camelCase naming.</summary>
    public static string SerializeEntity(T entity) => Serialize(entity);
    internal static List<T> DeserializeList(string json)
        => JsonSerializer.Deserialize<List<T>>(json, IndexedDbContext.JsonOptions) ?? new();
}
