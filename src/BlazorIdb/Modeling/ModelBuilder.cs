using BlazorIdb.Annotations;

namespace BlazorIdb.Modeling;

/// <summary>
/// Provides the versioned <c>OnModelCreating</c> configuration surface, analogous to
/// EF Core's <c>ModelBuilder</c>. Supports both simple (single-version) and fully
/// versioned (multi-version migration) schemas.
/// </summary>
public sealed class ModelBuilder
{
    private readonly Dictionary<int, VersionedModelBuilder> _versions = new();
    private readonly List<Type> _entityTypes = new();

    // Non-versioned entity builders go into "version 1" by default.
    private VersionedModelBuilder DefaultVersion => GetOrCreateVersion(1);

    /// <summary>
    /// Configures an entity type directly (without an explicit version).
    /// The configuration is applied to version 1 of the schema.
    /// </summary>
    /// <typeparam name="T">The CLR entity type.</typeparam>
    /// <param name="configure">Configuration callback.</param>
    public ModelBuilder Entity<T>(Action<EntityTypeBuilder<T>> configure) where T : class
    {
        DefaultVersion.Entity(configure);
        return this;
    }

    /// <summary>
    /// Declares a new schema version with an optional upgrade delegate.
    /// Versions must be declared in ascending order; gaps are not allowed.
    /// </summary>
    /// <param name="version">The version number (≥ 1).</param>
    /// <param name="configure">Callback that receives a <see cref="VersionedModelBuilder"/>
    /// for declaring store/index additions, deletions, and data migrations.</param>
    public ModelBuilder UseVersion(int version, Action<VersionedModelBuilder> configure)
    {
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be ≥ 1.");

        var vb = GetOrCreateVersion(version);
        configure(vb);
        return this;
    }

    /// <summary>
    /// Registers additional entity types that should be discovered even if they lack
    /// a matching <see cref="IndexedDbSet{T}"/> property on the context.
    /// </summary>
    public ModelBuilder RegisterEntity<T>() where T : class
    {
        _entityTypes.Add(typeof(T));
        return this;
    }

    // ------------------------------------------------------------------
    // Build
    // ------------------------------------------------------------------

    /// <summary>
    /// Injects a pre-built <see cref="StoreDefinition"/> from annotation scanning
    /// into version 1 of the model. Called internally before <see cref="OnModelCreating"/>;
    /// fluent configuration always takes precedence.
    /// </summary>
    internal void InjectAnnotatedStore(StoreDefinition def)
    {
        var v1 = DefaultVersion;
        if (!v1.EntityBuilders.ContainsKey(def.Name))
        {
            var accessor = new AnnotatedStoreAccessor(def);
            v1.EntityBuilders[def.Name] = accessor;
        }
    }

    /// <summary>
    /// Builds the final immutable <see cref="IndexedDbModel"/> from the accumulated
    /// configuration, using the highest registered version.
    /// </summary>
    public IndexedDbModel Build() => Build(0);

    /// <summary>
    /// Builds the final immutable <see cref="IndexedDbModel"/> from the accumulated
    /// configuration.
    /// </summary>
    public IndexedDbModel Build(int targetVersion)
    {
        // Ensure at least version 1 exists
        if (_versions.Count == 0)
            GetOrCreateVersion(1);

        var versionNumbers = _versions.Keys.OrderBy(v => v).ToList();
        var versionDefs = new List<VersionDefinition>();

        // Cumulative map: storeName → current StoreDefinition (for runtime metadata)
        var currentStores = new Dictionary<string, StoreDefinition>(StringComparer.Ordinal);

        foreach (var vNum in versionNumbers)
        {
            var vb = _versions[vNum];
            var vDef = new VersionDefinition { Version = vNum };

            // Stores registered in this VersionedModelBuilder
            foreach (var (storeName, eb) in vb.EntityBuilders)
            {
                var store = eb.GetDefinition();
                store.Name = storeName;

                if (!currentStores.ContainsKey(storeName))
                {
                    // Brand-new store in this version
                    vDef.CreateStores.Add(store);
                    currentStores[storeName] = store;
                }
                else
                {
                    // Existing store — merge index additions
                    var existingStore = currentStores[storeName];
                    var mod = new StoreModification { Name = storeName };
                    foreach (var idx in store.Indexes)
                    {
                        if (!existingStore.Indexes.Any(i => i.Name == idx.Name))
                        {
                            mod.AddIndexes.Add(idx);
                            existingStore.Indexes.Add(idx);
                        }
                    }
                    if (mod.AddIndexes.Count > 0 || mod.DeleteIndexes.Count > 0)
                        vDef.ModifyStores.Add(mod);
                }
            }

            // Explicitly deleted stores
            foreach (var del in vb.DeletedStores)
            {
                vDef.DeleteStores.Add(del);
                currentStores.Remove(del);
            }

            // Data migrations
            vDef.DataMigrations.AddRange(vb.DataMigrations);

            versionDefs.Add(vDef);
        }

        return new IndexedDbModel
        {
            Version = targetVersion > 0 ? targetVersion : (versionNumbers.LastOrDefault(1)),
            Versions = versionDefs,
            Stores = currentStores
        };
    }

    private VersionedModelBuilder GetOrCreateVersion(int version)
    {
        if (!_versions.TryGetValue(version, out var vb))
        {
            vb = new VersionedModelBuilder(version);
            _versions[version] = vb;
        }
        return vb;
    }
}

// ---- Accessor for annotation-injected stores (no generic needed) ----

internal sealed class AnnotatedStoreAccessor : IEntityBuilderAccessor
{
    private readonly StoreDefinition _definition;
    internal AnnotatedStoreAccessor(StoreDefinition definition) => _definition = definition;
    public StoreDefinition GetDefinition() => _definition;
}
