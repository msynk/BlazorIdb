namespace IdbBlazor.Annotations;

/// <summary>
/// Enables full-text search on the decorated string property.
/// IdbBlazor automatically tokenizes the value on write and stores the resulting
/// token array in a shadow property named <c>{PropertyName}_fts</c>, which is backed
/// by a <c>*</c> (multi-entry) IndexedDB index.  Use
/// <see cref="IndexedDbQuery{T}.Search(string)"/> to query across all full-text-indexed
/// properties on an entity.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IdbFullTextIndexAttribute : Attribute
{
    /// <summary>
    /// Gets the override name for the shadow field.
    /// Defaults to <c>{PropertyName}_fts</c>.
    /// </summary>
    public string? ShadowFieldName { get; set; }

    /// <summary>Initializes a new <see cref="IdbFullTextIndexAttribute"/>.</summary>
    /// <param name="shadowFieldName">
    /// Optional override for the auto-generated shadow field name.
    /// </param>
    public IdbFullTextIndexAttribute(string? shadowFieldName = null)
    {
        ShadowFieldName = shadowFieldName;
    }
}
