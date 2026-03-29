namespace BlazorIdb.Modeling;

/// <summary>
/// Fluent builder for configuring a specific IndexedDB index.
/// Returned by <see cref="EntityTypeBuilder{T}.HasIndex{TKey}"/> and
/// <see cref="EntityTypeBuilder{T}.HasUniqueIndex{TKey}"/>.
/// </summary>
/// <typeparam name="T">The owning entity type.</typeparam>
public sealed class IndexBuilder<T> where T : class
{
    private readonly EntityTypeBuilder<T> _parent;
    private readonly IndexDefinition _definition;

    internal IndexBuilder(EntityTypeBuilder<T> parent, IndexDefinition definition)
    {
        _parent = parent;
        _definition = definition;
    }

    /// <summary>Marks the index as unique.</summary>
    public IndexBuilder<T> IsUnique(bool unique = true)
    {
        _definition.Unique = unique;
        return this;
    }

    /// <summary>
    /// Marks the index as a multi-entry index.
    /// The keyPath must resolve to an array property on the entity; IndexedDB will
    /// create one entry per element.
    /// </summary>
    public IndexBuilder<T> IsMultiEntry(bool multiEntry = true)
    {
        _definition.MultiEntry = multiEntry;
        return this;
    }

    /// <summary>
    /// Enables automatic full-text tokenization for this index.
    /// BlazorIdb will maintain a shadow token-array field backed by this multi-entry index.
    /// </summary>
    public IndexBuilder<T> IsFullText()
    {
        _definition.MultiEntry = true;
        _definition.IsFullText = true;
        return this;
    }

    /// <summary>Returns the parent <see cref="EntityTypeBuilder{T}"/> for method chaining.</summary>
    public EntityTypeBuilder<T> Back() => _parent;
}
