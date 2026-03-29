namespace BlazorIdb.Annotations;

/// <summary>
/// Overrides the automatically derived object store name for an entity type.
/// By default, BlazorIdb uses the camelCase plural of the class name
/// (e.g., <c>Product</c> → <c>products</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class IdbStoreAttribute : Attribute
{
    /// <summary>Gets the explicit object store name.</summary>
    public string Name { get; }

    /// <summary>Initializes a new <see cref="IdbStoreAttribute"/>.</summary>
    /// <param name="name">The IndexedDB object store name.</param>
    public IdbStoreAttribute(string name)
    {
        Name = name;
    }
}
