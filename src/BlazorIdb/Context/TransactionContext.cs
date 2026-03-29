using System.Text.Json;
using BlazorIdb.Interop;

namespace BlazorIdb.Context;

/// <summary>
/// Provides typed, model-aware operation queuing for an IndexedDB transaction.
/// Obtain an instance via <see cref="IndexedDbContext.TransactionAsync"/>.
/// </summary>
public sealed class IndexedDbTransactionContext
{
    private readonly List<TransactionOperation> _operations = new();
    private readonly List<string> _stores = new();
    private readonly Func<Type, string> _storeNameResolver;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    internal IndexedDbTransactionContext(Func<Type, string> storeNameResolver)
    {
        _storeNameResolver = storeNameResolver;
    }

    /// <summary>
    /// Returns a store accessor for <typeparamref name="T"/> within this transaction.
    /// The store name is resolved automatically from the registered model.
    /// </summary>
    public TransactionSet<T> Set<T>() where T : class
    {
        var storeName = _storeNameResolver(typeof(T));
        EnsureStore(storeName);
        return new TransactionSet<T>(this, storeName);
    }

    internal void EnqueueAdd<T>(string storeName, T entity) where T : class
    {
        EnsureStore(storeName);
        _operations.Add(new TransactionOperation
        {
            Type = "add",
            Store = storeName,
            Value = JsonSerializer.Serialize(entity, _jsonOptions)
        });
    }

    internal void EnqueuePut<T>(string storeName, T entity) where T : class
    {
        EnsureStore(storeName);
        _operations.Add(new TransactionOperation
        {
            Type = "put",
            Store = storeName,
            Value = JsonSerializer.Serialize(entity, _jsonOptions)
        });
    }

    internal void EnqueueDelete(string storeName, object key)
    {
        EnsureStore(storeName);
        _operations.Add(new TransactionOperation
        {
            Type = "delete",
            Store = storeName,
            Key = JsonSerializer.Serialize(key)
        });
    }

    internal IReadOnlyList<TransactionOperation> Operations => _operations;
    internal IReadOnlyList<string> StoreNames => _stores;

    private void EnsureStore(string storeName)
    {
        if (!_stores.Contains(storeName, StringComparer.Ordinal))
            _stores.Add(storeName);
    }
}

/// <summary>
/// A strongly-typed store accessor for use within
/// <see cref="IndexedDbTransactionContext"/>. Operations are queued and
/// committed atomically when the transaction delegate completes.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class TransactionSet<T> where T : class
{
    private readonly IndexedDbTransactionContext _tx;
    private readonly string _storeName;

    internal TransactionSet(IndexedDbTransactionContext tx, string storeName)
    {
        _tx = tx;
        _storeName = storeName;
    }

    /// <summary>Queues an insert operation (fails if key already exists).</summary>
    public Task AddAsync(T entity)
    {
        _tx.EnqueueAdd(_storeName, entity);
        return Task.CompletedTask;
    }

    /// <summary>Queues an upsert operation (inserts or replaces).</summary>
    public Task PutAsync(T entity)
    {
        _tx.EnqueuePut(_storeName, entity);
        return Task.CompletedTask;
    }

    /// <summary>Queues a delete operation by primary key.</summary>
    public Task DeleteAsync(object key)
    {
        _tx.EnqueueDelete(_storeName, key);
        return Task.CompletedTask;
    }
}
