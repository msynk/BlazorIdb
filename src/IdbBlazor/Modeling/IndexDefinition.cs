using System.Text.Json.Serialization;

namespace IdbBlazor.Modeling;

/// <summary>Describes a single IndexedDB index within a store definition.</summary>
public sealed class IndexDefinition
{
    /// <summary>Gets or sets the index name (camelCase property path).</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the keyPath used by IndexedDB for this index.</summary>
    [JsonPropertyName("keyPath")]
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the index enforces uniqueness.</summary>
    [JsonPropertyName("unique")]
    public bool Unique { get; set; }

    /// <summary>Gets or sets whether this is a multi-entry index.</summary>
    [JsonPropertyName("multiEntry")]
    public bool MultiEntry { get; set; }

    /// <summary>Gets or sets whether this index supports full-text search.</summary>
    [JsonIgnore]
    public bool IsFullText { get; set; }
}
