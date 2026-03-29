using IdbBlazor.Annotations;

namespace IdbBlazor.Sample.Models;

/// <summary>Blog post entity demonstrating full-text search.</summary>
public sealed class BlogPost
{
    [IdbKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [IdbIndex]
    public string Title { get; set; } = string.Empty;

    [IdbIndex]
    public string Author { get; set; } = string.Empty;

    [IdbFullTextIndex]
    public string Body { get; set; } = string.Empty;

    // Shadow FTS field – maintained automatically by IdbBlazor
    public string[]? Body_fts { get; set; }

    [IdbIndex]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    [IdbMultiEntryIndex]
    public string[]? Tags { get; set; }
}
