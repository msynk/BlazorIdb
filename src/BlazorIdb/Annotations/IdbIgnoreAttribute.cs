namespace BlazorIdb.Annotations;

/// <summary>
/// Prevents BlazorIdb from persisting the decorated property to IndexedDB.
/// The property is excluded from both serialization and index discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IdbIgnoreAttribute : Attribute { }
