namespace BlazorIdb.Modeling;

/// <summary>
/// Defines a typed configuration class for an entity type, following the same
/// pattern as EF Core's <c>IEntityTypeConfiguration&lt;TEntity&gt;</c>.
/// </summary>
/// <typeparam name="T">The entity type to configure.</typeparam>
public interface IEntityTypeConfiguration<T> where T : class
{
    /// <summary>
    /// Configures the entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    void Configure(EntityTypeBuilder<T> builder);
}
