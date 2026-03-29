using System.Text.Json.Serialization;

namespace BlazorIdb.Modeling;

/// <summary>
/// The fully built, immutable schema model passed from .NET to the JS layer and used
/// at runtime to resolve store names, key properties, and indexes for query translation.
/// </summary>
public sealed class IndexedDbModel
{
    /// <summary>Gets the target database version.</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; }

    /// <summary>Gets the version delta list serialized and sent to the JS layer.</summary>
    [JsonPropertyName("versions")]
    public IReadOnlyList<VersionDefinition> Versions { get; init; } = Array.Empty<VersionDefinition>();

    /// <summary>
    /// Gets all store definitions across all versions, keyed by store name.
    /// This is the merged/current-state view used at runtime for metadata lookups.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, StoreDefinition> Stores { get; init; }
        = new Dictionary<string, StoreDefinition>();

    /// <summary>
    /// Gets the store name for the given CLR entity type, or throws if unknown.
    /// </summary>
    public string GetStoreName(Type entityType)
    {
        foreach (var kv in Stores)
        {
            if (kv.Value.EntityType == entityType)
                return kv.Key;
        }
        throw new InvalidOperationException(
            $"No IndexedDB store is registered for entity type '{entityType.Name}'. " +
            $"Ensure it is configured in OnModelCreating or carries an [IdbKey] annotation " +
            $"on at least one property.");
    }

    /// <summary>
    /// Gets the store definition for the given CLR entity type.
    /// </summary>
    public StoreDefinition GetStore(Type entityType)
    {
        foreach (var kv in Stores)
        {
            if (kv.Value.EntityType == entityType)
                return kv.Value;
        }
        throw new InvalidOperationException(
            $"No IndexedDB store is registered for entity type '{entityType.Name}'.");
    }

    /// <summary>
    /// Returns <c>true</c> if the given CLR entity type is registered in this model.
    /// </summary>
    public bool HasStore(Type entityType)
        => Stores.Values.Any(s => s.EntityType == entityType);
}
