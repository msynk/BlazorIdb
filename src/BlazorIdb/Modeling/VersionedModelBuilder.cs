using BlazorIdb.Annotations;

namespace BlazorIdb.Modeling;

/// <summary>
/// Configuration surface for a single schema version.
/// Returned by <see cref="ModelBuilder.UseVersion(int, Action{VersionedModelBuilder})"/>.
/// </summary>
public sealed class VersionedModelBuilder
{
    internal int Version { get; }
    internal Dictionary<string, IEntityBuilderAccessor> EntityBuilders { get; } = new(StringComparer.Ordinal);
    internal List<string> DeletedStores { get; } = new();
    internal List<(Type EntityType, Func<IMigrationStore, Task> Action)> DataMigrations { get; } = new();

    internal VersionedModelBuilder(int version)
    {
        Version = version;
    }

    /// <summary>
    /// Configures an entity type within this version.
    /// </summary>
    /// <typeparam name="T">The CLR entity type.</typeparam>
    /// <param name="configure">Configuration callback.</param>
    public VersionedModelBuilder Entity<T>(Action<EntityTypeBuilder<T>> configure) where T : class
    {
        var storeName = NamingHelper.GetStoreName(typeof(T));
        if (!EntityBuilders.TryGetValue(storeName, out var accessor))
        {
            var def = new StoreDefinition
            {
                Name = storeName,
                EntityType = typeof(T)
            };
            accessor = new EntityBuilderAccessor<T>(def);
            EntityBuilders[storeName] = accessor;
        }
        else if (accessor is not EntityBuilderAccessor<T>)
        {
            // Upgrade annotation-injected accessor to a typed one so fluent config can merge in
            var typedAccessor = new EntityBuilderAccessor<T>(accessor.GetDefinition());
            accessor = typedAccessor;
            EntityBuilders[storeName] = accessor;
        }

        configure(((EntityBuilderAccessor<T>)accessor).Builder);
        return this;
    }

    /// <summary>
    /// Marks an existing store for deletion during this version upgrade.
    /// </summary>
    public VersionedModelBuilder DeleteStore(string storeName)
    {
        DeletedStores.Add(storeName);
        return this;
    }

    /// <summary>
    /// Registers a data migration callback for entity type <typeparamref name="T"/>.
    /// The callback receives a lightweight store accessor for reading and writing
    /// records. It runs <em>after</em> the database has been upgraded to this version.
    /// </summary>
    /// <typeparam name="T">The entity type to migrate.</typeparam>
    /// <param name="migrate">
    /// Async callback that performs the data migration using the provided
    /// <see cref="IMigrationStore"/>.
    /// </param>
    public VersionedModelBuilder Migrate<T>(Func<IMigrationStore, Task> migrate) where T : class
    {
        DataMigrations.Add((typeof(T), migrate));
        return this;
    }
}

// ---- internal helper to manage typed builders without generics ----

internal interface IEntityBuilderAccessor
{
    StoreDefinition GetDefinition();
}

internal sealed class EntityBuilderAccessor<T> : IEntityBuilderAccessor where T : class
{
    internal EntityTypeBuilder<T> Builder { get; }
    private readonly StoreDefinition _definition;

    internal EntityBuilderAccessor(StoreDefinition definition)
    {
        _definition = definition;
        Builder = new EntityTypeBuilder<T>(definition);
    }

    public StoreDefinition GetDefinition() => _definition;
}
