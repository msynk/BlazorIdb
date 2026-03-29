using System.Text.Json;
using System.Text.Json.Serialization;
using IdbBlazor.Modeling;

namespace IdbBlazor.Interop;

/// <summary>
/// Serializes an <see cref="IndexedDbModel"/> to the JSON schema format expected by
/// <c>blazordb.js</c>.
/// </summary>
internal static class JsSchemaSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Produces the JSON schema string to pass to <c>IdbBlazor.openDatabase()</c>.
    /// </summary>
    internal static string Serialize(IndexedDbModel model)
    {
        var payload = new SchemaPayload
        {
            Versions = model.Versions
                .Select(v => new VersionPayload
                {
                    Version = v.Version,
                    CreateStores = v.CreateStores.Select(s => new CreateStorePayload
                    {
                        Name = s.Name,
                        KeyPath = s.KeyPath,
                        AutoIncrement = s.AutoIncrement,
                        Indexes = s.Indexes.Select(MapIndex).ToList()
                    }).ToList(),
                    ModifyStores = v.ModifyStores.Select(m => new ModifyStorePayload
                    {
                        Name = m.Name,
                        AddIndexes = m.AddIndexes.Select(MapIndex).ToList(),
                        DeleteIndexes = m.DeleteIndexes
                    }).ToList(),
                    DeleteStores = v.DeleteStores
                })
                .ToList()
        };

        return JsonSerializer.Serialize(payload, _options);
    }

    private static IndexPayload MapIndex(IndexDefinition idx) => new()
    {
        Name = idx.Name,
        KeyPath = idx.KeyPath,
        Unique = idx.Unique,
        MultiEntry = idx.MultiEntry
    };

    // ---- anonymous-style payload records ----

    private sealed class SchemaPayload
    {
        public List<VersionPayload> Versions { get; set; } = new();
    }

    private sealed class VersionPayload
    {
        public int Version { get; set; }
        public List<CreateStorePayload> CreateStores { get; set; } = new();
        public List<ModifyStorePayload> ModifyStores { get; set; } = new();
        public List<string> DeleteStores { get; set; } = new();
    }

    private sealed class CreateStorePayload
    {
        public string Name { get; set; } = string.Empty;
        public string? KeyPath { get; set; }
        public bool AutoIncrement { get; set; }
        public List<IndexPayload> Indexes { get; set; } = new();
    }

    private sealed class ModifyStorePayload
    {
        public string Name { get; set; } = string.Empty;
        public List<IndexPayload> AddIndexes { get; set; } = new();
        public List<string> DeleteIndexes { get; set; } = new();
    }

    private sealed class IndexPayload
    {
        public string Name { get; set; } = string.Empty;
        public string KeyPath { get; set; } = string.Empty;
        public bool Unique { get; set; }
        public bool MultiEntry { get; set; }
    }
}
