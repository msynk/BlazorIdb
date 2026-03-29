using BlazorIdb.Annotations;

namespace BlazorIdb.Sample.Models;

/// <summary>Sample product entity demonstrating all BlazorIdb annotation types.</summary>
public sealed class Product
{
    [IdbKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [IdbIndex]
    public string? Category { get; set; }

    [IdbIndex]
    public decimal Price { get; set; }

    [IdbUniqueIndex]
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [IdbFullTextIndex]
    public string? Description { get; set; }

    // Shadow FTS field – maintained automatically by BlazorIdb
    public string[]? Description_fts { get; set; }

    public string? ImageUrl { get; set; }

    [IdbMultiEntryIndex]
    public string[]? Tags { get; set; }
}
