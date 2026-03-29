namespace IdbBlazor.Annotations;

/// <summary>
/// Creates an IndexedDB index on the decorated property.
/// Multiple <see cref="IdbIndexAttribute"/> instances may be applied to the same property
/// only when combined with <see cref="AttributeTargets.Property"/> and unique names.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IdbIndexAttribute : Attribute
{
    /// <summary>
    /// Gets the explicit index name. When <c>null</c>, the camelCase property name is used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets a value indicating whether the index enforces uniqueness.</summary>
    public bool Unique { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a multi-entry index.
    /// A multi-entry index (where the keyPath points to an array property) creates one
    /// index entry per element in the array, enabling efficient querying for
    /// &quot;contains&quot; semantics over collections.
    /// </summary>
    public bool MultiEntry { get; set; }

    /// <summary>Initializes a new <see cref="IdbIndexAttribute"/>.</summary>
    /// <param name="name">Optional explicit index name; defaults to the camelCase property name.</param>
    public IdbIndexAttribute(string? name = null)
    {
        Name = name;
    }
}
