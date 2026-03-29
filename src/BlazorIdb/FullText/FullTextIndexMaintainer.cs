using System.Reflection;
using System.Text.Json;
using BlazorIdb.Annotations;
using BlazorIdb.Modeling;

namespace BlazorIdb.FullText;

/// <summary>
/// Keeps full-text shadow fields up to date whenever an entity with
/// <see cref="IdbFullTextIndexAttribute"/>-decorated properties is persisted.
/// Called automatically by <see cref="IndexedDbSet{T}.AddAsync"/> and
/// <see cref="IndexedDbSet{T}.UpdateAsync"/>.
/// </summary>
public static class FullTextIndexMaintainer
{
    /// <summary>
    /// For each <see cref="IdbFullTextIndexAttribute"/>-annotated property on
    /// <typeparamref name="T"/>, computes the token array and writes it into the
    /// corresponding shadow property via reflection.
    /// </summary>
    /// <remarks>
    /// Shadow properties are expected to be <c>string[]?</c> CLR properties whose
    /// names match the shadow field name configured on the attribute or the default
    /// <c>{PropertyName}_Fts</c> (PascalCase for the CLR; camelCase in IDB).
    /// If the shadow property does not exist on the CLR type, tokens are injected
    /// at serialization time via <see cref="InjectTokensIntoJson"/>.
    /// </remarks>
    public static void Maintain<T>(T entity, StoreDefinition store) where T : class
    {
        var type = typeof(T);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<IdbFullTextIndexAttribute>();
            if (attr is null) continue;

            var text = prop.GetValue(entity) as string;
            var tokens = Tokenizer.Tokenize(text);

            // Derive the CLR shadow property name (PascalCase)
            var shadowCamel = attr.ShadowFieldName
                ?? Modeling.NamingHelper.ToCamelCase(prop.Name) + "_fts";
            var shadowPascal = char.ToUpperInvariant(shadowCamel[0]) + shadowCamel[1..];

            // Try to set a matching CLR property
            var shadowProp = type.GetProperty(shadowPascal,
                BindingFlags.Public | BindingFlags.Instance);
            if (shadowProp is null)
            {
                // Try the exact camelCase name (in case the developer named it that way)
                shadowProp = type.GetProperty(shadowCamel,
                    BindingFlags.Public | BindingFlags.Instance);
            }

            shadowProp?.SetValue(entity, tokens);
        }
    }

    /// <summary>
    /// Injects FTS token arrays into an already-serialized JSON document.
    /// Used when the shadow property is absent on the CLR type but must still
    /// appear in the IndexedDB record so the multi-entry index can be maintained.
    /// </summary>
    public static string InjectTokensIntoJson(string json, StoreDefinition store)
    {
        // Fast-path: no FTS indexes on this store
        var ftsIndexes = store.Indexes.Where(i => i.IsFullText).ToList();
        if (ftsIndexes.Count == 0) return json;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
            dict[prop.Name] = prop.Value;

        var modified = false;
        foreach (var ftsIdx in ftsIndexes)
        {
            // The shadow field name is the index name itself (e.g. "content_fts")
            var shadowField = ftsIdx.Name;
            // Find the source property (strip the _fts suffix)
            var sourcePropCamel = shadowField.EndsWith("_fts", StringComparison.Ordinal)
                ? shadowField[..^4]
                : shadowField;

            if (!dict.TryGetValue(sourcePropCamel, out var sourceElem)) continue;

            var text = sourceElem.ValueKind == JsonValueKind.String
                ? sourceElem.GetString()
                : sourceElem.ToString();

            var tokens = Tokenizer.Tokenize(text);
            var tokenJson = JsonSerializer.SerializeToDocument(tokens).RootElement;
            dict[shadowField] = tokenJson;
            modified = true;
        }

        return modified ? JsonSerializer.Serialize(dict) : json;
    }
}
