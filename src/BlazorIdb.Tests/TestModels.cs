using BlazorIdb.Annotations;

namespace BlazorIdb.Tests;

// ---- Common test entity models ----

/// <summary>Product entity used across multiple test classes.</summary>
public sealed class Product
{
    [IdbKey]
    public int Id { get; set; }

    [IdbIndex]
    public string Category { get; set; } = string.Empty;

    [IdbIndex]
    public decimal Price { get; set; }

    [IdbUniqueIndex]
    public string Sku { get; set; } = string.Empty;

    [IdbFullTextIndex]
    public string Description { get; set; } = string.Empty;

    /// <summary>Shadow field for FTS tokens — maintained automatically by BlazorIdb.</summary>
    public string[]? Description_fts { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>Blog post entity used for transaction and FTS tests.</summary>
public sealed class BlogPost
{
    [IdbKey]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    [IdbFullTextIndex]
    public string Content { get; set; } = string.Empty;

    public string[]? Content_fts { get; set; }

    [IdbIndex]
    public string AuthorId { get; set; } = string.Empty;
}

/// <summary>Simple entity with only convention-based configuration (no annotations).</summary>
public sealed class OrderItem
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>Entity with complex tag list and multi-entry index.</summary>
public sealed class TaggedDocument
{
    [IdbKey]
    public string Id { get; set; } = string.Empty;

    [IdbMultiEntryIndex]
    public string[] Tags { get; set; } = Array.Empty<string>();

    public string Body { get; set; } = string.Empty;
}
