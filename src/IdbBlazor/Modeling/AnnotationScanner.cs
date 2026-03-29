using IdbBlazor.Annotations;
using System.Reflection;

namespace IdbBlazor.Modeling;

/// <summary>
/// Scans a CLR type for IdbBlazor data annotations and populates a
/// <see cref="StoreDefinition"/> accordingly.  Called automatically by
/// <see cref="IndexedDbContext"/> before <c>OnModelCreating</c>, so fluent
/// configuration always wins over annotations.
/// </summary>
public static class AnnotationScanner
{
    /// <summary>
    /// Builds a <see cref="StoreDefinition"/> from the annotations on <paramref name="entityType"/>.
    /// Returns <c>null</c> if the type carries no IdbBlazor annotations at all.
    /// </summary>
    public static StoreDefinition? ScanType(Type entityType)
    {
        var hasAnnotation = false;
        var storeName = NamingHelper.GetStoreName(entityType);

        // [IdbStore] override
        var storeAttr = entityType.GetCustomAttribute<IdbStoreAttribute>();
        if (storeAttr != null)
        {
            storeName = storeAttr.Name;
            hasAnnotation = true;
        }

        var def = new StoreDefinition
        {
            Name = storeName,
            EntityType = entityType
        };

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip ignored
            if (prop.GetCustomAttribute<IdbIgnoreAttribute>() != null) continue;

            // Primary key
            var keyAttr = prop.GetCustomAttribute<IdbKeyAttribute>();
            if (keyAttr != null)
            {
                def.KeyPropertyName = prop.Name;
                def.KeyPath = NamingHelper.ToCamelCase(prop.Name);
                def.AutoIncrement = keyAttr.AutoIncrement;
                hasAnnotation = true;
                continue;
            }

            // Unique index
            var uniqueAttr = prop.GetCustomAttribute<IdbUniqueIndexAttribute>();
            if (uniqueAttr != null)
            {
                var idxName = uniqueAttr.Name ?? NamingHelper.ToCamelCase(prop.Name);
                def.Indexes.Add(new IndexDefinition
                {
                    Name = idxName,
                    KeyPath = NamingHelper.ToCamelCase(prop.Name),
                    Unique = true
                });
                hasAnnotation = true;
                continue;
            }

            // MultiEntry index
            var multiAttr = prop.GetCustomAttribute<IdbMultiEntryIndexAttribute>();
            if (multiAttr != null)
            {
                var idxName = multiAttr.Name ?? NamingHelper.ToCamelCase(prop.Name);
                def.Indexes.Add(new IndexDefinition
                {
                    Name = idxName,
                    KeyPath = NamingHelper.ToCamelCase(prop.Name),
                    MultiEntry = true
                });
                hasAnnotation = true;
                continue;
            }

            // Full-text index
            var ftsAttr = prop.GetCustomAttribute<IdbFullTextIndexAttribute>();
            if (ftsAttr != null)
            {
                var shadowField = ftsAttr.ShadowFieldName
                    ?? NamingHelper.ToCamelCase(prop.Name) + "_fts";
                def.Indexes.Add(new IndexDefinition
                {
                    Name = shadowField,
                    KeyPath = shadowField,
                    MultiEntry = true,
                    IsFullText = true
                });
                hasAnnotation = true;
                continue;
            }

            // Regular index
            var idxAttr = prop.GetCustomAttribute<IdbIndexAttribute>();
            if (idxAttr != null)
            {
                var idxName = idxAttr.Name ?? NamingHelper.ToCamelCase(prop.Name);
                def.Indexes.Add(new IndexDefinition
                {
                    Name = idxName,
                    KeyPath = NamingHelper.ToCamelCase(prop.Name),
                    Unique = idxAttr.Unique,
                    MultiEntry = idxAttr.MultiEntry
                });
                hasAnnotation = true;
            }
        }

        // Apply key convention if no explicit key found but annotations exist
        if (hasAnnotation && string.IsNullOrEmpty(def.KeyPath))
            ApplyKeyConvention(entityType, def);

        return hasAnnotation ? def : null;
    }

    /// <summary>Applies convention-based key detection (Id / TypeNameId).</summary>
    internal static void ApplyKeyConvention(Type entityType, StoreDefinition def)
    {
        var conventionNames = new[] { "Id", $"{entityType.Name}Id" };
        foreach (var name in conventionNames)
        {
            var prop = entityType.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                def.KeyPropertyName = prop.Name;
                def.KeyPath = NamingHelper.ToCamelCase(prop.Name);
                return;
            }
        }
    }
}
