namespace BlazorIdb.Interop;

/// <summary>
/// Describes a single native IndexedDB query for the query translator.
/// A null <see cref="IndexName"/> means the primary key is used (store.getAll / store.get).
/// </summary>
public sealed class NativeQueryDescriptor
{
    /// <summary>
    /// The IndexedDB index name to query, or <c>null</c> to query by primary key.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>The key range to apply. <c>null</c> means retrieve all records.</summary>
    public IdbKeyRange? Range { get; set; }

    /// <summary>
    /// Whether to return only unique primary keys from the index (to avoid duplicates
    /// in multi-entry queries before an in-memory dedup).
    /// </summary>
    public bool UniqueKeys { get; set; }
}
