using IdbBlazor.Modeling;
using IdbBlazor.Sample.Models;
using Microsoft.JSInterop;

namespace IdbBlazor.Sample.Data;

/// <summary>
/// Application IndexedDB context demonstrating versioned schema migrations.
/// </summary>
public sealed class AppDb : IndexedDbContext
{
    /// <summary>The products object store.</summary>
    public IndexedDbSet<Product> Products { get; set; } = null!;

    /// <summary>The blog posts object store.</summary>
    public IndexedDbSet<BlogPost> BlogPosts { get; set; } = null!;

    /// <inheritdoc/>
    public AppDb(IJSRuntime jsRuntime) : base(jsRuntime) { }

    /// <inheritdoc/>
    public AppDb(IJSRuntime jsRuntime, IdbBlazor.Options.IndexedDbOptions options)
        : base(jsRuntime, options) { }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Version 1: initial schema
        builder.UseVersion(1, v =>
        {
            v.Entity<Product>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Category);
                e.HasUniqueIndex(x => x.Sku);
                e.HasIndex(x => x.Description).IsFullText();
                e.HasIndex(x => x.Tags).IsMultiEntry();
            });

            v.Entity<BlogPost>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Author);
                e.HasIndex(x => x.PublishedAt);
                e.HasIndex(x => x.Body).IsFullText();
                e.HasIndex(x => x.Tags).IsMultiEntry();
            });
        });

        // Version 2: add Price index + backfill missing prices
        builder.UseVersion(2, v =>
        {
            v.Entity<Product>(e =>
            {
                e.HasIndex(x => x.Price);
            });

            v.Migrate<Product>(async store =>
            {
                var allRaw = await store.GetAllRawAsync();
                foreach (var raw in allRaw)
                {
                    // In a real migration you would deserialize, transform, and re-save.
                    // This is a no-op placeholder for demonstration purposes.
                    await store.PutRawAsync(raw);
                }
            });
        });
    }
}
