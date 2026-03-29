using System.Text.Json.Serialization;

namespace IdbBlazor.Modeling;

/// <summary>
/// Describes the schema for a single object store as it should be created or modified
/// during an IndexedDB version upgrade.
/// </summary>
public sealed class StoreDefinition
{
    /// <summary>Gets or sets the object store name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the keyPath (camelCase property name). Null for out-of-line keys.</summary>
    [JsonPropertyName("keyPath")]
    public string? KeyPath { get; set; }

    /// <summary>Gets or sets whether the key auto-increments.</summary>
    [JsonPropertyName("autoIncrement")]
    public bool AutoIncrement { get; set; }

    /// <summary>Gets the indexes to create on this store.</summary>
    [JsonPropertyName("indexes")]
    public List<IndexDefinition> Indexes { get; set; } = new();

    // ---- used at runtime (not serialized to JS) ----

    /// <summary>The CLR entity type this store holds.</summary>
    [JsonIgnore]
    public Type? EntityType { get; set; }

    /// <summary>The CLR property name (PascalCase) of the key.</summary>
    [JsonIgnore]
    public string? KeyPropertyName { get; set; }

    /// <summary>Data migration callbacks registered for this store.</summary>
    [JsonIgnore]
    public List<Func<IMigrationStore, Task>> Migrations { get; set; } = new();
}
