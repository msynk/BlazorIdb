using System.Text.Json;
using BlazorIdb.Options;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorIdb.Tests;

/// <summary>
/// Integration-style tests for <see cref="IndexedDbContext"/> and
/// <see cref="IndexedDbSet{T}"/> using a mock JS runtime.
/// </summary>
public sealed class IndexedDbContextTests
{
    // ---- Test context ----

    private sealed class TestDb : IndexedDbContext
    {
        public IndexedDbSet<Product> Products { get; set; } = null!;
        public IndexedDbSet<BlogPost> BlogPosts { get; set; } = null!;

        public TestDb(IJSRuntime js) : base(js) { }
        public TestDb(IJSRuntime js, IndexedDbOptions opts) : base(js, opts) { }

        protected override void OnModelCreating(Modeling.ModelBuilder builder)
        {
            builder.Entity<Product>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Category);
            });
        }
    }

    // ---- Helpers ----

    private static MockJsRuntime BuildMock()
    {
        var mock = new MockJsRuntime();
        // Return from openDatabase
        mock.Returns("BlazorIdb.openDatabase",
            new { oldVersion = 0, newVersion = 1 });
        return mock;
    }

    private static string SerializeList<T>(IEnumerable<T> items)
        => JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    // ---- Tests ----

    [Fact]
    public async Task EnsureInitializedAsync_CallsOpenDatabase()
    {
        var mock = BuildMock();
        var db = new TestDb(mock);

        await db.EnsureInitializedAsync();

        var call = mock.Calls.FirstOrDefault(c => c.Identifier == "BlazorIdb.openDatabase");
        Assert.NotNull(call);
    }

    [Fact]
    public async Task Set_ReturnsPropertyInstance()
    {
        var mock = BuildMock();
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        var set = db.Set<Product>();
        Assert.NotNull(set);
        Assert.Same(db.Products, set);
    }

    [Fact]
    public async Task AddAsync_CallsJsAdd()
    {
        var mock = BuildMock();
        // InvokeAsync<string> for add returns serialized key
        mock.Returns("BlazorIdb.add", "1");
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        var product = new Product { Id = 1, Category = "Electronics", Price = 99.99m, Sku = "SKU-1" };
        await db.Products.AddAsync(product);

        Assert.Contains(mock.Calls, c => c.Identifier == "BlazorIdb.add");
    }

    [Fact]
    public async Task FindAsync_CallsJsGet_AndDeserializes()
    {
        var mock = BuildMock();
        var expected = new Product { Id = 1, Category = "Electronics", Price = 49.99m, Sku = "SKU-1" };
        mock.Returns("BlazorIdb.get", JsonSerializer.Serialize(expected,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        var result = await db.Products.FindAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
        Assert.Equal("Electronics", result.Category);
    }

    [Fact]
    public async Task ToListAsync_CallsJsGetAll_AndDeserializes()
    {
        var mock = BuildMock();
        var data = new[]
        {
            new Product { Id = 1, Category = "A", Sku = "S1" },
            new Product { Id = 2, Category = "B", Sku = "S2" }
        };
        mock.Returns("BlazorIdb.getAll", SerializeList(data));
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        var list = await db.Products.ToListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("A", list[0].Category);
    }

    [Fact]
    public async Task Where_NativeTranslation_CallsGetAllByIndex()
    {
        var mock = BuildMock();
        var data = new[]
        {
            new Product { Id = 1, Category = "Electronics", Sku = "S1" }
        };
        mock.Returns("BlazorIdb.getAllByIndex", SerializeList(data));
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        var list = await db.Products
            .Where(x => x.Category == "Electronics")
            .ToListAsync();

        Assert.Contains(mock.Calls, c => c.Identifier == "BlazorIdb.getAllByIndex");
        Assert.Single(list);
    }

    [Fact]
    public async Task DeleteAsync_CallsJsDelete()
    {
        var mock = BuildMock();
        mock.Returns("BlazorIdb.delete", true);
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        await db.Products.DeleteAsync(1);

        Assert.Contains(mock.Calls, c => c.Identifier == "BlazorIdb.delete");
    }

    [Fact]
    public async Task TransactionAsync_ExecutesMultipleOpsAtomically()
    {
        var mock = BuildMock();
        mock.Returns("BlazorIdb.executeTransaction", "[]");
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        await db.TransactionAsync(async tx =>
        {
            var products = tx.Set<Product>();
            var posts = tx.Set<BlogPost>();

            await products.PutAsync(new Product { Id = 1, Category = "Tech", Sku = "T1" });
            await posts.PutAsync(new BlogPost { Id = "p1", Title = "Hello", AuthorId = "a1" });
        });

        Assert.Contains(mock.Calls, c => c.Identifier == "BlazorIdb.executeTransaction");
    }

    [Fact]
    public async Task CountAsync_ReturnsJsCount()
    {
        var mock = BuildMock();
        mock.Returns("BlazorIdb.count", 42);
        var db = new TestDb(mock);
        await db.EnsureInitializedAsync();

        var count = await db.Products.CountAsync();
        Assert.Equal(42, count);
    }

    [Fact]
    public async Task Options_OverridesDatabaseName()
    {
        var mock = BuildMock();
        var opts = new IndexedDbOptions().UseDatabase("CustomDb", 2);
        var db = new TestDb(mock, opts);
        await db.EnsureInitializedAsync();

        var call = mock.Calls.First(c => c.Identifier == "BlazorIdb.openDatabase");
        Assert.Equal("CustomDb", call.Args?[0]?.ToString());
        Assert.Equal(2, (int)call.Args![1]!);
    }
}
