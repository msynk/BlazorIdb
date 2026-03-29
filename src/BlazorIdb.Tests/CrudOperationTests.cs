using System.Text.Json;
using BlazorIdb.FullText;
using Xunit;

namespace BlazorIdb.Tests;

/// <summary>Tests for FTS token maintenance and CRUD serialization.</summary>
public sealed class CrudOperationTests
{
    [Fact]
    public void FullTextMaintainer_PopulatesShadowField()
    {
        var product = new Product
        {
            Id = 1,
            Description = "High quality electronics component",
            Sku = "X1"
        };

        var storeDef = new Modeling.StoreDefinition
        {
            Name = "products",
            EntityType = typeof(Product),
            KeyPath = "id"
        };
        storeDef.Indexes.Add(new Modeling.IndexDefinition
        {
            Name = "description_fts",
            KeyPath = "description_fts",
            MultiEntry = true,
            IsFullText = true
        });

        FullTextIndexMaintainer.Maintain(product, storeDef);

        Assert.NotNull(product.Description_fts);
        Assert.Contains("high", product.Description_fts!);
        Assert.Contains("quality", product.Description_fts!);
        Assert.Contains("electronics", product.Description_fts!);
        Assert.Contains("component", product.Description_fts!);
    }

    [Fact]
    public void FullTextMaintainer_InjectTokens_WhenShadowPropertyAbsent()
    {
        var json = JsonSerializer.Serialize(
            new { id = 1, content = "fast and reliable" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var storeDef = new Modeling.StoreDefinition { Name = "docs", KeyPath = "id" };
        storeDef.Indexes.Add(new Modeling.IndexDefinition
        {
            Name = "content_fts",
            KeyPath = "content_fts",
            MultiEntry = true,
            IsFullText = true
        });

        var result = FullTextIndexMaintainer.InjectTokensIntoJson(json, storeDef);

        Assert.Contains("content_fts", result);
        Assert.Contains("fast", result);
        Assert.Contains("reliable", result);
    }

    [Fact]
    public void Tokenize_ProducesExpectedTokens()
    {
        var tokens = Tokenizer.Tokenize("The quick, brown FOX jumps over the lazy dog!");

        Assert.Contains("quick", tokens);
        Assert.Contains("brown", tokens);
        Assert.Contains("fox", tokens);
        Assert.Contains("jumps", tokens);
        Assert.Contains("over", tokens);
        Assert.Contains("lazy", tokens);
        Assert.Contains("dog", tokens);
        // Deduplication: "the" appears twice → once in set
        Assert.Single(tokens.Where(t => t == "the"));
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesValues()
    {
        var original = new Product
        {
            Id = 99,
            Category = "Test",
            Price = 3.14m,
            Sku = "SKU-99",
            Name = "Widget"
        };

        var json = IndexedDbSet<Product>.SerializeEntity(original);
        var restored = IndexedDbSet<Product>.Deserialize(json);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Category, restored.Category);
        Assert.Equal(original.Price, restored.Price);
        Assert.Equal(original.Sku, restored.Sku);
        Assert.Equal(original.Name, restored.Name);
    }

    [Fact]
    public void Serialization_UsesCamelCase()
    {
        var product = new Product { Id = 1, Category = "A", Sku = "S" };
        var json = IndexedDbSet<Product>.SerializeEntity(product);

        Assert.Contains("\"category\"", json);
        Assert.Contains("\"sku\"", json);
        Assert.Contains("\"price\"", json);
        Assert.DoesNotContain("\"Category\"", json);
    }
}
