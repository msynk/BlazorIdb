namespace BlazorIdb.Modeling;

/// <summary>
/// Represents the read/write surface of a single object store made available
/// to data-migration callbacks so they can read existing records, transform them,
/// and write them back — all using plain .NET types.
/// </summary>
public interface IMigrationStore
{
    /// <summary>Returns all records in the store as JSON strings.</summary>
    Task<List<string>> GetAllRawAsync();

    /// <summary>Puts (inserts or replaces) a JSON-serialized record.</summary>
    Task PutRawAsync(string json);

    /// <summary>Deletes the record with the given JSON-serialized key.</summary>
    Task DeleteRawAsync(string keyJson);
}
