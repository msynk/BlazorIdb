namespace BlazorIdb.Options;

/// <summary>
/// Configuration options for an <see cref="IndexedDbContext"/>.
/// Pass an instance to <see cref="DependencyInjection.ServiceCollectionExtensions.AddIndexedDb{TContext}(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{IndexedDbOptions}?)"/>
/// to override the convention-based database name and version.
/// </summary>
public sealed class IndexedDbOptions
{
    /// <summary>Gets the database name, or <c>null</c> to use the context class name.</summary>
    public string? DatabaseName { get; private set; }

    /// <summary>Gets the target version, or <c>null</c> to use 1.</summary>
    public int? Version { get; private set; }

    /// <summary>
    /// Configures the database name and target version.
    /// </summary>
    /// <param name="name">The IndexedDB database name.</param>
    /// <param name="version">The target schema version (≥ 1).</param>
    public IndexedDbOptions UseDatabase(string name, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Database name must not be empty.", nameof(name));
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Version must be ≥ 1.");

        DatabaseName = name;
        Version = version;
        return this;
    }
}
