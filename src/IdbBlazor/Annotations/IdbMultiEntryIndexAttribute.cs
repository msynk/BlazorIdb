namespace IdbBlazor.Annotations;

/// <summary>
/// Creates a <em>multi-entry</em> IndexedDB index on the decorated array/collection property.
/// Each element in the array is indexed individually, enabling efficient &quot;array contains&quot;
/// queries. Use <see cref="IndexedDbQuery{T}.WhereArrayContains"/> or a
/// <c>.Where(x => x.Tags.Contains(tag))</c> predicate (automatically translated).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IdbMultiEntryIndexAttribute : Attribute
{
    /// <summary>
    /// Gets the explicit index name. When <c>null</c>, the camelCase property name is used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Initializes a new <see cref="IdbMultiEntryIndexAttribute"/>.</summary>
    /// <param name="name">Optional explicit index name.</param>
    public IdbMultiEntryIndexAttribute(string? name = null)
    {
        Name = name;
    }
}
