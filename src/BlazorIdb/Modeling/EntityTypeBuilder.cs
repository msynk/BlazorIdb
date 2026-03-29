using System.Linq.Expressions;
using System.Reflection;
using BlazorIdb.Annotations;

namespace BlazorIdb.Modeling;

/// <summary>
/// Provides a fluent API for configuring an IndexedDB object store that maps to
/// entity type <typeparamref name="T"/>. The API is intentionally modelled after
/// EF Core's <c>EntityTypeBuilder&lt;T&gt;</c> so that developers familiar with
/// Entity Framework feel immediately at home.
/// </summary>
/// <typeparam name="T">The CLR entity type.</typeparam>
public sealed class EntityTypeBuilder<T> where T : class
{
    internal StoreDefinition Definition { get; }

    internal EntityTypeBuilder(StoreDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>
    /// Configures the primary key for this store using a property selector.
    /// </summary>
    /// <param name="keySelector">Expression selecting the key property.</param>
    /// <param name="autoIncrement">
    /// When <c>true</c>, IndexedDB auto-increments the key on insert.
    /// </param>
    public EntityTypeBuilder<T> HasKey<TKey>(
        Expression<Func<T, TKey>> keySelector,
        bool autoIncrement = false)
    {
        var prop = GetProperty(keySelector);
        Definition.KeyPropertyName = prop.Name;
        Definition.KeyPath = NamingHelper.ToCamelCase(prop.Name);
        Definition.AutoIncrement = autoIncrement;
        return this;
    }

    /// <summary>Creates a regular (non-unique, single-entry) index on a property.</summary>
    /// <param name="indexSelector">Expression selecting the indexed property.</param>
    public IndexBuilder<T> HasIndex<TKey>(Expression<Func<T, TKey>> indexSelector)
    {
        var prop = GetProperty(indexSelector);
        var indexName = NamingHelper.ToCamelCase(prop.Name);
        var def = EnsureIndex(indexName, indexName);
        return new IndexBuilder<T>(this, def);
    }

    /// <summary>Creates a unique index on a property.</summary>
    public IndexBuilder<T> HasUniqueIndex<TKey>(Expression<Func<T, TKey>> indexSelector)
    {
        var prop = GetProperty(indexSelector);
        var indexName = NamingHelper.ToCamelCase(prop.Name);
        var def = EnsureIndex(indexName, indexName);
        def.Unique = true;
        return new IndexBuilder<T>(this, def);
    }

    /// <summary>
    /// Sets the IndexedDB object store name, overriding the convention-based default.
    /// </summary>
    public EntityTypeBuilder<T> ToTable(string storeName)
    {
        Definition.Name = storeName;
        return this;
    }

    /// <summary>Applies a separate <see cref="IEntityTypeConfiguration{T}"/> instance.</summary>
    public EntityTypeBuilder<T> Apply(IEntityTypeConfiguration<T> configuration)
    {
        configuration.Configure(this);
        return this;
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private static PropertyInfo GetProperty<TKey>(Expression<Func<T, TKey>> selector)
    {
        var body = selector.Body;
        // Unwrap Convert() casts that compilers add for value types
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } conv)
            body = conv.Operand;

        if (body is MemberExpression { Member: PropertyInfo pi })
            return pi;

        throw new ArgumentException(
            $"Expression '{selector}' does not select a single property of '{typeof(T).Name}'.",
            nameof(selector));
    }

    private IndexDefinition EnsureIndex(string name, string keyPath)
    {
        var existing = Definition.Indexes.FirstOrDefault(i => i.Name == name);
        if (existing != null) return existing;
        var def = new IndexDefinition { Name = name, KeyPath = keyPath };
        Definition.Indexes.Add(def);
        return def;
    }
}
