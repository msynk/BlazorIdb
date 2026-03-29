using IdbBlazor.Modeling;
using Xunit;

namespace IdbBlazor.Tests;

/// <summary>Tests for the fluent ModelBuilder and annotation scanning.</summary>
public sealed class ModelBuilderTests
{
    [Fact]
    public void Entity_FluientKey_SetsKeyPath()
    {
        var builder = new ModelBuilder();
        builder.Entity<Product>(e => e.HasKey(x => x.Id));
        var model = builder.Build(1);

        Assert.True(model.HasStore(typeof(Product)));
        var store = model.GetStore(typeof(Product));
        Assert.Equal("id", store.KeyPath);
    }

    [Fact]
    public void Entity_FluentIndex_AddsIndex()
    {
        var builder = new ModelBuilder();
        builder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Category);
        });

        var model = builder.Build(1);
        var store = model.GetStore(typeof(Product));

        Assert.Contains(store.Indexes, i => i.Name == "category");
    }

    [Fact]
    public void AnnotationScanner_DetectsIdbKeyAndIndex()
    {
        var def = AnnotationScanner.ScanType(typeof(Product))!;

        Assert.NotNull(def);
        Assert.Equal("id", def.KeyPath);
        Assert.Contains(def.Indexes, i => i.Name == "category");
        Assert.Contains(def.Indexes, i => i.Name == "price");
        Assert.Contains(def.Indexes, i => i.Name == "sku" && i.Unique);
    }

    [Fact]
    public void AnnotationScanner_DetectsFullTextIndex()
    {
        var def = AnnotationScanner.ScanType(typeof(Product))!;
        Assert.Contains(def.Indexes, i => i.IsFullText && i.MultiEntry);
    }

    [Fact]
    public void AnnotationScanner_DetectsMultiEntryIndex()
    {
        var def = AnnotationScanner.ScanType(typeof(TaggedDocument))!;
        Assert.Contains(def.Indexes, i => i.Name == "tags" && i.MultiEntry);
    }

    [Fact]
    public void StoreName_Convention_PluralisesCorrectly()
    {
        Assert.Equal("products", NamingHelper.GetStoreName(typeof(Product)));
        Assert.Equal("blogPosts", NamingHelper.GetStoreName(typeof(BlogPost)));
        Assert.Equal("orderItems", NamingHelper.GetStoreName(typeof(OrderItem)));
        Assert.Equal("taggedDocuments", NamingHelper.GetStoreName(typeof(TaggedDocument)));
    }

    [Fact]
    public void UseVersion_BuildsVersionedModel()
    {
        var builder = new ModelBuilder();
        builder.UseVersion(1, v =>
        {
            v.Entity<Product>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Category);
            });
        });
        builder.UseVersion(2, v =>
        {
            v.Entity<Product>(e => e.HasIndex(x => x.Price));
        });

        var model = builder.Build(2);

        Assert.Equal(2, model.Versions.Count);
        Assert.Single(model.Versions[0].CreateStores);
        Assert.Single(model.Versions[1].ModifyStores);
        var mod = model.Versions[1].ModifyStores[0];
        Assert.Contains(mod.AddIndexes, i => i.Name == "price");
    }

    [Fact]
    public void IEntityTypeConfiguration_IsAppliedByApplyMethod()
    {
        var builder = new ModelBuilder();
        builder.Entity<Product>(e => e.Apply(new ProductConfig()));

        var model = builder.Build(1);
        var store = model.GetStore(typeof(Product));
        Assert.Equal("id", store.KeyPath);
        Assert.Contains(store.Indexes, i => i.Name == "category");
    }

    private sealed class ProductConfig : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Category);
        }
    }
}
