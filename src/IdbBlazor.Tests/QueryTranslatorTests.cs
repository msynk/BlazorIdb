using System.Linq.Expressions;
using IdbBlazor.Interop;
using IdbBlazor.Modeling;
using IdbBlazor.Query;
using Xunit;

namespace IdbBlazor.Tests;

/// <summary>Unit tests for <see cref="QueryTranslator"/>.</summary>
public sealed class QueryTranslatorTests
{
    private static StoreDefinition MakeProductStore()
    {
        var store = new StoreDefinition
        {
            Name = "products",
            EntityType = typeof(Product),
            KeyPath = "id",
            KeyPropertyName = "Id"
        };
        store.Indexes.Add(new IndexDefinition { Name = "category", KeyPath = "category" });
        store.Indexes.Add(new IndexDefinition { Name = "price", KeyPath = "price" });
        store.Indexes.Add(new IndexDefinition { Name = "sku", KeyPath = "sku", Unique = true });
        return store;
    }

    [Fact]
    public void Translate_Equality_ReturnsCorrectIndexAndRange()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Category == "Electronics";

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Equal("category", result!.IndexName);
        Assert.Equal("Electronics", result.Range!.Only);
    }

    [Fact]
    public void Translate_GreaterThan_ReturnsLowerBound()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Price > 10m;

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Equal("price", result!.IndexName);
        // Lower bound, open (exclusive >)
        Assert.Equal(10m, result.Range!.Lower);
        Assert.True(result.Range.LowerOpen);
    }

    [Fact]
    public void Translate_LessThanOrEqual_ReturnsUpperBound()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Price <= 99.99m;

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Equal("price", result!.IndexName);
        Assert.Equal(99.99m, result.Range!.Upper);
        Assert.False(result.Range.UpperOpen);
    }

    [Fact]
    public void Translate_AndAlso_BoundedRange_MergesBounds()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Price > 10m && x.Price <= 100m;

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Equal("price", result!.IndexName);
        Assert.Equal(10m, result.Range!.Lower);
        Assert.True(result.Range.LowerOpen);
        Assert.Equal(100m, result.Range.Upper);
        Assert.False(result.Range.UpperOpen);
    }

    [Fact]
    public void Translate_StartsWith_ReturnsPrefixRange()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Category.StartsWith("Elec");

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Equal("category", result!.IndexName);
        Assert.Equal("Elec", result.Range!.Lower);
        Assert.Equal("Elec\uffff", result.Range.Upper);
    }

    [Fact]
    public void Translate_NonIndexedProperty_ReturnsNull()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Name == "Widget";

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.Null(result); // Name has no index
    }

    [Fact]
    public void Translate_PrimaryKeyEquality_ReturnsNullIndexName()
    {
        var store = MakeProductStore();
        Expression<Func<Product, bool>> pred = x => x.Id == 42;

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Null(result!.IndexName); // primary key — no index name needed
    }

    [Fact]
    public void Translate_ClosureVariable_EvaluatesCorrectly()
    {
        var store = MakeProductStore();
        var target = "Electronics";
        Expression<Func<Product, bool>> pred = x => x.Category == target;

        var result = QueryTranslator.TryTranslate(pred, store);

        Assert.NotNull(result);
        Assert.Equal("Electronics", result!.Range!.Only);
    }
}
