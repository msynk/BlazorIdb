namespace BlazorIdb.Annotations;

/// <summary>
/// Marks a property as the primary key for its IndexedDB object store.
/// By convention, a property named <c>Id</c> or <c>{TypeName}Id</c> is automatically
/// treated as the key; use this attribute to override the convention or configure
/// auto-increment behavior.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IdbKeyAttribute : Attribute
{
    /// <summary>
    /// Gets a value indicating whether the key should be auto-incremented by IndexedDB.
    /// When <c>true</c>, the key property should be a nullable integer so that unset
    /// values trigger auto-increment on insert.
    /// </summary>
    public bool AutoIncrement { get; }

    /// <summary>Initializes a new <see cref="IdbKeyAttribute"/>.</summary>
    /// <param name="autoIncrement">
    /// Set to <c>true</c> to enable IndexedDB auto-increment for this key.
    /// </param>
    public IdbKeyAttribute(bool autoIncrement = false)
    {
        AutoIncrement = autoIncrement;
    }
}
