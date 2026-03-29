namespace BlazorIdb.Modeling;

/// <summary>
/// Shared helpers for deriving store names and camelCase property paths.
/// </summary>
public static class NamingHelper
{
    /// <summary>
    /// Converts a PascalCase property name to its camelCase equivalent used by IndexedDB.
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length == 1) return name.ToLowerInvariant();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Derives the default IndexedDB object store name for a CLR entity type.
    /// Returns the camelCase plural of the type name
    /// (e.g., <c>Product</c> → <c>products</c>, <c>BlogPost</c> → <c>blogPosts</c>).
    /// </summary>
    public static string GetStoreName(Type entityType)
    {
        var name = ToCamelCase(entityType.Name);

        // Simple English pluralisation
        if (name.EndsWith("s", StringComparison.Ordinal)
            || name.EndsWith("x", StringComparison.Ordinal)
            || name.EndsWith("z", StringComparison.Ordinal)
            || name.EndsWith("ch", StringComparison.Ordinal)
            || name.EndsWith("sh", StringComparison.Ordinal))
            return name + "es";

        if (name.EndsWith("y", StringComparison.Ordinal) && name.Length > 1
            && !IsVowel(name[^2]))
            return name[..^1] + "ies";

        return name + "s";
    }

    private static bool IsVowel(char c)
        => "aeiouAEIOU".Contains(c, StringComparison.Ordinal);
}
