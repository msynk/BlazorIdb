using Microsoft.JSInterop;
using System.Text.Json;

namespace BlazorIdb.Interop;

/// <summary>
/// Thin .NET wrapper around the <c>window.BlazorIdb</c> JavaScript object defined in
/// <c>blazordb.js</c>.  All public members are <c>async</c>; no synchronous paths exist.
/// </summary>
public sealed class IndexedDbJsInterop
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes a new <see cref="IndexedDbJsInterop"/>.</summary>
    public IndexedDbJsInterop(IJSRuntime js)
    {
        _js = js;
    }

    // ---- Lifecycle ----

    /// <summary>
    /// Opens the database at the requested version, running schema upgrades as needed.
    /// </summary>
    /// <returns>A tuple of (oldVersion, newVersion).</returns>
    public async Task<(int OldVersion, int NewVersion)> OpenDatabaseAsync(
        string dbName, int version, string schemaJson)
    {
        var result = await _js.InvokeAsync<JsonElement>(
            "BlazorIdb.openDatabase", dbName, version, schemaJson);
        var oldV = result.GetProperty("oldVersion").GetInt32();
        var newV = result.GetProperty("newVersion").GetInt32();
        return (oldV, newV);
    }

    /// <summary>Permanently deletes the named database.</summary>
    public Task DeleteDatabaseAsync(string dbName)
        => _js.InvokeAsync<bool>("BlazorIdb.deleteDatabase", dbName).AsTask();

    // ---- CRUD ----

    /// <summary>Adds a new record. Returns the resulting key as a JSON string.</summary>
    public Task<string> AddAsync(string dbName, string storeName, string valueJson)
        => _js.InvokeAsync<string>("BlazorIdb.add", dbName, storeName, valueJson).AsTask();

    /// <summary>Puts (inserts or replaces) a record. Returns the resulting key as a JSON string.</summary>
    public Task<string> PutAsync(string dbName, string storeName, string valueJson)
        => _js.InvokeAsync<string>("BlazorIdb.put", dbName, storeName, valueJson).AsTask();

    /// <summary>
    /// Gets a record by primary key.
    /// Returns <c>null</c> if not found.
    /// </summary>
    public Task<string?> GetAsync(string dbName, string storeName, string keyJson)
        => _js.InvokeAsync<string?>("BlazorIdb.get", dbName, storeName, keyJson).AsTask();

    /// <summary>Deletes a record by primary key.</summary>
    public Task DeleteAsync(string dbName, string storeName, string keyJson)
        => _js.InvokeAsync<bool>("BlazorIdb.delete", dbName, storeName, keyJson).AsTask();

    /// <summary>Returns all records from a store, optionally filtered by key range.</summary>
    public Task<string> GetAllAsync(string dbName, string storeName, IdbKeyRange? range = null)
    {
        var rangeJson = range != null ? JsonSerializer.Serialize(range, _json) : null;
        return _js.InvokeAsync<string>("BlazorIdb.getAll", dbName, storeName, rangeJson).AsTask();
    }

    /// <summary>Returns all records matching an index key range.</summary>
    public Task<string> GetAllByIndexAsync(
        string dbName, string storeName, string indexName, IdbKeyRange range)
    {
        var rangeJson = JsonSerializer.Serialize(range, _json);
        return _js.InvokeAsync<string>("BlazorIdb.getAllByIndex",
            dbName, storeName, indexName, rangeJson).AsTask();
    }

    /// <summary>Returns all primary keys from a store (optionally by range).</summary>
    public Task<string> GetAllKeysAsync(string dbName, string storeName, IdbKeyRange? range = null)
    {
        var rangeJson = range != null ? JsonSerializer.Serialize(range, _json) : null;
        return _js.InvokeAsync<string>("BlazorIdb.getAllKeys", dbName, storeName, rangeJson).AsTask();
    }

    /// <summary>Returns all primary keys matching an index key range.</summary>
    public Task<string> GetAllKeysByIndexAsync(
        string dbName, string storeName, string indexName, IdbKeyRange range)
    {
        var rangeJson = JsonSerializer.Serialize(range, _json);
        return _js.InvokeAsync<string>("BlazorIdb.getAllKeysByIndex",
            dbName, storeName, indexName, rangeJson).AsTask();
    }

    // ---- Aggregates ----

    /// <summary>Returns the number of records in a store.</summary>
    public Task<int> CountAsync(string dbName, string storeName)
        => _js.InvokeAsync<int>("BlazorIdb.count", dbName, storeName).AsTask();

    /// <summary>Returns the number of records matching an index key range.</summary>
    public Task<int> CountByIndexAsync(
        string dbName, string storeName, string indexName, IdbKeyRange range)
    {
        var rangeJson = JsonSerializer.Serialize(range, _json);
        return _js.InvokeAsync<int>("BlazorIdb.countByIndex",
            dbName, storeName, indexName, rangeJson).AsTask();
    }

    /// <summary>Removes all records from a store.</summary>
    public Task ClearAsync(string dbName, string storeName)
        => _js.InvokeAsync<bool>("BlazorIdb.clear", dbName, storeName).AsTask();

    // ---- Transactions ----

    /// <summary>
    /// Executes an ordered list of <see cref="TransactionOperation"/>s within a
    /// single IndexedDB transaction.
    /// </summary>
    /// <returns>
    /// A JSON array where each element is the result of the corresponding operation
    /// (the result key for writes; the record JSON for reads; <c>null</c> for deletes).
    /// </returns>
    public Task<string> ExecuteTransactionAsync(
        string dbName,
        IEnumerable<string> storeNames,
        string mode,
        IEnumerable<TransactionOperation> operations)
    {
        var storeNamesJson = JsonSerializer.Serialize(storeNames.Distinct().ToArray());
        var opsJson = JsonSerializer.Serialize(operations.ToArray(), _json);
        return _js.InvokeAsync<string>("BlazorIdb.executeTransaction",
            dbName, storeNamesJson, mode, opsJson).AsTask();
    }

    // ---- Live Query ----

    /// <summary>
    /// Subscribes to change notifications on a store via polling.
    /// The <paramref name="dotnetRef"/> is invoked with the JSON-serialized store
    /// contents whenever a change is detected.
    /// </summary>
    /// <returns>A subscription ID that can be passed to <see cref="UnsubscribeAsync"/>.</returns>
    public Task<int> SubscribeToStoreAsync(
        string dbName, string storeName,
        DotNetObjectReference<object> dotnetRef,
        string callbackMethod,
        int intervalMs = 500)
        => _js.InvokeAsync<int>("BlazorIdb.subscribeToStore",
            dbName, storeName, dotnetRef, callbackMethod, intervalMs).AsTask();

    /// <summary>Cancels a store subscription.</summary>
    public Task UnsubscribeAsync(int subscriptionId)
        => _js.InvokeVoidAsync("BlazorIdb.unsubscribeFromStore", subscriptionId).AsTask();
}

/// <summary>An individual operation within a batched IndexedDB transaction.</summary>
public sealed class TransactionOperation
{
    /// <summary>Operation type: <c>add</c>, <c>put</c>, <c>delete</c>, <c>get</c>, <c>getAll</c>.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>The target object store name.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("store")]
    public string Store { get; set; } = string.Empty;

    /// <summary>JSON-serialized record value (for <c>add</c> / <c>put</c>).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    /// <summary>JSON-serialized primary key (for <c>get</c> / <c>delete</c>).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("key")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; set; }
}
