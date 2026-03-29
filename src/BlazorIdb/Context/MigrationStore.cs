using System.Text.Json;
using BlazorIdb.Interop;
using BlazorIdb.Modeling;

namespace BlazorIdb.Context;

/// <summary>
/// Provides the raw read/write surface for a single object store used during
/// data-migration callbacks.  Operations execute as individual transactions;
/// they are not meant for high-throughput use.
/// </summary>
internal sealed class MigrationStore : IMigrationStore
{
    private readonly IndexedDbJsInterop _js;
    private readonly string _dbName;
    private readonly string _storeName;

    internal MigrationStore(IndexedDbJsInterop js, string dbName, string storeName)
    {
        _js = js;
        _dbName = dbName;
        _storeName = storeName;
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAllRawAsync()
    {
        var json = await _js.GetAllAsync(_dbName, _storeName);
        var elements = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? new();
        return elements.Select(e => e.GetRawText()).ToList();
    }

    /// <inheritdoc/>
    public Task PutRawAsync(string json)
        => _js.PutAsync(_dbName, _storeName, json);

    /// <inheritdoc/>
    public Task DeleteRawAsync(string keyJson)
        => _js.DeleteAsync(_dbName, _storeName, keyJson);
}
