namespace IdbBlazor.Annotations;

/// <summary>
/// Creates a <em>unique</em> IndexedDB index on the decorated property.
/// This is a convenience shorthand that combines <see cref="IdbIndexAttribute"/> with
/// <c>Unique = true</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IdbUniqueIndexAttribute : Attribute
{
    /// <summary>
    /// Gets the explicit index name. When <c>null</c>, the camelCase property name is used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Initializes a new <see cref="IdbUniqueIndexAttribute"/>.</summary>
    /// <param name="name">Optional explicit index name.</param>
    public IdbUniqueIndexAttribute(string? name = null)
    {
        Name = name;
    }
}
