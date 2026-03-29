using System.Text.Json.Serialization;

namespace IdbBlazor.Modeling;

/// <summary>
/// Describes the schema delta applied when upgrading the database to a specific version.
/// The JS layer reads these deltas in ascending version order inside
/// <c>onupgradeneeded</c>.
/// </summary>
public sealed class VersionDefinition
{
    /// <summary>Gets or sets the IndexedDB version number (1-based, monotonically increasing).</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>Gets the object stores to <em>create</em> during this version upgrade.</summary>
    [JsonPropertyName("createStores")]
    public List<StoreDefinition> CreateStores { get; set; } = new();

    /// <summary>Gets the object stores to <em>modify</em> (add/remove indexes) during this version upgrade.</summary>
    [JsonPropertyName("modifyStores")]
    public List<StoreModification> ModifyStores { get; set; } = new();

    /// <summary>Gets the names of object stores to <em>delete</em> entirely during this version upgrade.</summary>
    [JsonPropertyName("deleteStores")]
    public List<string> DeleteStores { get; set; } = new();

    // ---- runtime only ----

    /// <summary>
    /// Data-migration callbacks scoped to this version.
    /// These are executed after the database has been upgraded to this version.
    /// </summary>
    [JsonIgnore]
    public List<(Type EntityType, Func<IMigrationStore, Task> Action)> DataMigrations { get; set; } = new();
}
