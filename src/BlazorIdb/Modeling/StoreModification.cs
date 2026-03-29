using System.Text.Json.Serialization;

namespace BlazorIdb.Modeling;

/// <summary>
/// Represents a store modification applied when upgrading to a particular version:
/// indexes to add and indexes to remove from an existing store.
/// </summary>
public sealed class StoreModification
{
    /// <summary>Gets or sets the name of the store being modified.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the index definitions to add to this store.</summary>
    [JsonPropertyName("addIndexes")]
    public List<IndexDefinition> AddIndexes { get; set; } = new();

    /// <summary>Gets the index names to remove from this store.</summary>
    [JsonPropertyName("deleteIndexes")]
    public List<string> DeleteIndexes { get; set; } = new();
}
