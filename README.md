# BlazorIdb

An EF Core–style IndexedDB wrapper for Blazor WebAssembly. Define your data model with attributes or fluent API, get typed `DbSet`-like stores, LINQ queries, full-text search, live queries, and versioned schema migrations — all backed by the browser's native IndexedDB.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Defining a Context](#defining-a-context)
- [Annotations Reference](#annotations-reference)
- [Schema Modeling (Fluent API)](#schema-modeling-fluent-api)
- [CRUD Operations](#crud-operations)
- [Querying](#querying)
- [Full-Text Search](#full-text-search)
- [Transactions](#transactions)
- [Live Queries](#live-queries)
- [Migrations & Versioning](#migrations--versioning)
- [Source Generator](#source-generator)
- [Project Structure](#project-structure)

---

## Features

- **EF Core–style context** — derive from `IndexedDbContext`, declare `IndexedDbSet<T>` properties, override `OnModelCreating`
- **Attribute-driven or fluent schema configuration** — both styles work together
- **CRUD & bulk operations** — `AddAsync`, `UpdateAsync`, `PutAsync`, `DeleteAsync`, `FindAsync`, `ClearAsync`, `CountAsync`, ...
- **Composable LINQ queries** — `Where`, `OrderBy`, `Skip`, `Take` with native IndexedDB push-down for equality, range, prefix, and bounded-range predicates
- **Full-text search** — tokenized multi-entry indexes with AND-semantics across multiple terms
- **Atomic multi-store transactions** — wrap operations across multiple stores in a single `TransactionAsync` call
- **Reactive live queries** — `IObservable<IEnumerable<T>>` backed by lightweight JS polling
- **Versioned schema migrations** — add/remove stores and indexes per version, with data migration callbacks
- **Roslyn source generator** — compile-time context validation
- **Zero runtime dependencies** beyond the ASP.NET WebAssembly SDK

---

## Installation

> **Prerequisites:** Blazor WebAssembly project targeting .NET 8+

1. Add the NuGet package:

   ```bash
   dotnet add package BlazorIdb
   ```

2. Add the script to `wwwroot/index.html` before the closing `</body>` tag:

   ```html
   <script src="_content/BlazorIdb/blazordb.js"></script>
   ```

---

## Quick Start

**1. Define your model:**

```csharp
using BlazorIdb.Annotations;

public class Product
{
    [IdbKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    [IdbIndex]
    public string? Category { get; set; }

    [IdbUniqueIndex]
    public string Sku { get; set; } = "";
}
```

**2. Create a context:**

```csharp
using BlazorIdb.Context;
using BlazorIdb.Modeling;
using BlazorIdb.Options;

public sealed class AppDb : IndexedDbContext
{
    public IndexedDbSet<Product> Products { get; set; } = null!;

    public AppDb(IJSRuntime js) : base(js) { }
    public AppDb(IJSRuntime js, IndexedDbOptions opts) : base(js, opts) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.UseVersion(1, v =>
        {
            v.Entity<Product>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasUniqueIndex(x => x.Sku);
                e.HasIndex(x => x.Category);
            });
        });
    }
}
```

**3. Register in `Program.cs`:**

```csharp
builder.Services.AddIndexedDb<AppDb>(opts =>
    opts.UseDatabase("MyApp", version: 1));
```

**4. Use in a component:**

```razor
@inject AppDb Db
@implements IDisposable

@code {
    private List<Product> _products = new();
    private IDisposable? _sub;

    protected override async Task OnInitializedAsync()
    {
        await Db.EnsureInitializedAsync();

        _sub = Db.Products
            .LiveQuery()
            .Subscribe(items =>
            {
                _products = items.ToList();
                InvokeAsync(StateHasChanged);
            });
    }

    private async Task AddProduct(Product p) => await Db.Products.AddAsync(p);
    private async Task DeleteProduct(string id) => await Db.Products.DeleteAsync(id);

    public void Dispose() => _sub?.Dispose();
}
```

---

## Defining a Context

Derive from `IndexedDbContext` and declare your stores as `IndexedDbSet<T>` properties. Override `OnModelCreating` to configure the schema using versioned blocks. The context is registered as a scoped service.

```csharp
public sealed class AppDb : IndexedDbContext
{
    public IndexedDbSet<Product>  Products  { get; set; } = null!;
    public IndexedDbSet<BlogPost> BlogPosts { get; set; } = null!;

    public AppDb(IJSRuntime js) : base(js) { }
    public AppDb(IJSRuntime js, IndexedDbOptions opts) : base(js, opts) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
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
                e.HasIndex(x => x.Body).IsFullText();
                e.HasIndex(x => x.PublishedAt);
            });
        });
    }
}
```

Always call `EnsureInitializedAsync()` before any store access (e.g., in `OnInitializedAsync`). The method is idempotent — it is safe to call multiple times.

---

## Annotations Reference

Attributes can replace or supplement the fluent API. Both approaches are fully composable.

| Attribute | Target | Description |
|---|---|---|
| `[IdbKey(autoIncrement?)]` | Property | Primary key. Set `autoIncrement = true` for integer auto-increment keys. Convention: `Id` or `{TypeName}Id` is detected automatically. |
| `[IdbStore(name)]` | Class | Override the default store name (defaults to camelCase plural, e.g. `Product` → `products`). |
| `[IdbIndex(name?)]` | Property | Regular (non-unique, single-entry) index. |
| `[IdbUniqueIndex(name?)]` | Property | Unique index — insertion throws if the value already exists. |
| `[IdbMultiEntryIndex(name?)]` | Property | One index entry per element of an array/collection property, enabling "array contains" queries. |
| `[IdbFullTextIndex(shadowField?)]` | Property | Tokenizes the property value into a `{PropertyName}_fts` shadow `string[]` field backed by a multi-entry index, enabling full-text search. |
| `[IdbIgnore]` | Property | Exclude the property from serialization and index discovery. |
| `[IdbNativeOnly]` | Class | Require all query predicates to be translated to native IndexedDB operations; throws `IdbNativeQueryException` on fallback. |

**Example — annotation-based model:**

```csharp
[IdbStore("products")]
public class Product
{
    [IdbKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    [IdbIndex]
    public string? Category { get; set; }

    [IdbIndex]
    public decimal Price { get; set; }

    [IdbUniqueIndex]
    public string Sku { get; set; } = "";

    [IdbFullTextIndex]
    public string? Description { get; set; }
    public string[]? Description_fts { get; set; } // shadow field, auto-maintained

    [IdbMultiEntryIndex]
    public string[]? Tags { get; set; }
}
```

---

## Schema Modeling (Fluent API)

All fluent configuration happens inside `OnModelCreating` through versioned blocks:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.UseVersion(1, v =>
    {
        v.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);                            // primary key
            e.HasIndex(x => x.Category);                    // regular index
            e.HasUniqueIndex(x => x.Sku);                   // unique index
            e.HasIndex(x => x.Tags).IsMultiEntry();         // multi-entry index
            e.HasIndex(x => x.Description).IsFullText();    // full-text index
            e.ToTable("products");                           // custom store name
        });
    });
}
```

Use `IEntityTypeConfiguration<T>` to separate configuration into its own class (mirrors EF Core's `IEntityTypeConfiguration`):

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasUniqueIndex(x => x.Sku);
    }
}

// In OnModelCreating:
v.Entity<Product>(e => e.Apply(new ProductConfiguration()));
```

---

## CRUD Operations

All operations are `async` and return `Task` or `Task<T>`.

```csharp
// Insert (throws on duplicate key)
await Db.Products.AddAsync(product);

// Insert or replace
await Db.Products.PutAsync(product);

// Update existing
await Db.Products.UpdateAsync(product);

// Find by primary key
var product = await Db.Products.FindAsync("some-id");

// Delete by primary key
await Db.Products.DeleteAsync("some-id");

// Get all records
var all = await Db.Products.ToListAsync();

// Count records
var count = await Db.Products.CountAsync();

// Remove all records
await Db.Products.ClearAsync();
```

---

## Querying

`IndexedDbSet<T>` provides both a quick `Where` shorthand and a full composable query builder.

```csharp
// Shorthand — executed with native IDB push-down when possible
var electronics = await Db.Products
    .Where(p => p.Category == "Electronics")
    .ToListAsync();

// Full query builder
var page = await Db.Products
    .AsQueryable()
    .Where(p => p.Price > 100m)
    .OrderByDescending(p => p.Price)
    .Skip(20).Take(20)
    .ToListAsync();

// Other terminal methods
var first  = await Db.Products.AsQueryable().Where(...).FirstOrDefaultAsync();
var single = await Db.Products.AsQueryable().Where(...).SingleOrDefaultAsync();
var count  = await Db.Products.AsQueryable().Where(...).CountAsync();
var exists = await Db.Products.AsQueryable().Where(...).AnyAsync();
```

### Native IndexedDB push-down

The query translator automatically pushes supported predicates to IndexedDB, avoiding a full in-memory scan:

| C# predicate | IDB operation |
|---|---|
| `x.Prop == value` | `IDBKeyRange.only(value)` |
| `x.Prop > value` / `>=` / `<` / `<=` | `IDBKeyRange.lowerBound` / `upperBound` |
| `x.Prop > lb && x.Prop < ub` | `IDBKeyRange.bound` |
| `x.Prop.StartsWith("prefix")` | `IDBKeyRange.bound(prefix, prefix + '\uffff')` |

Predicates that cannot be translated fall back to an in-memory scan transparently. To opt out of silently falling back, use `.AsNativeOnly()` (or `[IdbNativeOnly]` on the class) — this causes an `IdbNativeQueryException` to be thrown instead.

---

## Full-Text Search

Declare a full-text index (fluent or annotation), add an optional `string[]` shadow field, and use `Search`:

```csharp
// Model
public class BlogPost
{
    [IdbKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [IdbFullTextIndex]
    public string Body { get; set; } = "";
    public string[]? Body_fts { get; set; } // shadow field — auto-maintained
}

// Query — multiple terms are AND-ed
var results = await Db.BlogPosts.Search("blazor performance").ToListAsync();

// Compose with other operators
var results = await Db.BlogPosts
    .AsQueryable()
    .Search("blazor")
    .OrderByDescending(p => p.PublishedAt)
    .Take(10)
    .ToListAsync();
```

**How it works:** On each write (`AddAsync`, `UpdateAsync`, `PutAsync`) the library tokenizes the source property (lowercase → split on whitespace/punctuation → deduplicate → sort → drop tokens shorter than 2 characters) and writes the token array into the shadow field. The shadow field is stored in a multi-entry IndexedDB index, so each token gets its own index entry. Search terms are individually looked up via that index and the results are intersected.

---

## Transactions

Wrap operations across one or more stores in a single atomic transaction:

```csharp
await Db.TransactionAsync(async tx =>
{
    var products = tx.Set<Product>();
    var posts    = tx.Set<BlogPost>();

    await products.AddAsync(new Product { ... });
    await products.AddAsync(new Product { ... });
    await posts.AddAsync(new BlogPost { ... });
});
```

If any operation inside the callback throws, the entire transaction is rolled back by IndexedDB automatically.

---

## Live Queries

`LiveQuery()` returns an `IObservable<IEnumerable<T>>` that re-emits whenever the underlying store changes. It is backed by a JS polling interval (500 ms by default) and a `DotNetObjectReference` callback.

```csharp
@implements IDisposable

@code {
    private List<Product> _products = new();
    private IDisposable? _sub;

    protected override async Task OnInitializedAsync()
    {
        await Db.EnsureInitializedAsync();

        _sub = Db.Products
            .LiveQuery(p => p.Category == "Electronics") // optional filter
            .Subscribe(items =>
            {
                _products = items.ToList();
                InvokeAsync(StateHasChanged);
            });
    }

    public void Dispose() => _sub?.Dispose(); // clears the JS polling interval
}
```

`Subscribe(Action<IEnumerable<T>>)` is a convenience overload that avoids a dependency on `System.Reactive`. Disposing the subscription calls `BlazorIdb.unsubscribe(subId)` in JS.

`ToLiveQuery()` is also available as a terminal operator on `IndexedDbQuery<T>`.

---

## Migrations & Versioning

Each `UseVersion(n, ...)` block declares the schema delta to apply when upgrading from the previous version to `n`. Blocks are applied incrementally — a fresh install runs all versions in order, an upgrade from version 1 to 3 runs only versions 2 and 3.

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    // Version 1 — initial schema
    builder.UseVersion(1, v =>
    {
        v.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Category);
            e.HasUniqueIndex(x => x.Sku);
        });
    });

    // Version 2 — add a new index and backfill data
    builder.UseVersion(2, v =>
    {
        v.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Price);
        });

        v.Migrate<Product>(async store =>
        {
            var all = await store.GetAllRawAsync();
            foreach (var rawJson in all)
            {
                // transform rawJson as needed, then re-put
                await store.PutRawAsync(rawJson);
            }
        });
    });

    // Version 3 — drop a store
    builder.UseVersion(3, v =>
    {
        v.DeleteStore("legacyStore");
    });
}
```

Register the final version in DI:

```csharp
builder.Services.AddIndexedDb<AppDb>(opts =>
    opts.UseDatabase("MyApp", version: 3));
```

---

## Source Generator

The `BlazorIdb.SourceGenerators` package is automatically included with `BlazorIdb`. It is a Roslyn incremental source generator that discovers all `IndexedDbContext` subclasses at compile time and emits a `partial class` stub for each, enabling compile-time validation of context configuration.

No additional setup is required — it runs automatically as part of the build.

---

## Project Structure

```
src/
├── BlazorIdb/                   # Core library
│   ├── Annotations/             # [IdbKey], [IdbIndex], [IdbStore], etc.
│   ├── Context/                 # IndexedDbContext, IndexedDbSet<T>, transaction types
│   ├── DependencyInjection/     # AddIndexedDb<T> extension
│   ├── FullText/                # Tokenizer and FTS index maintainer
│   ├── Interop/                 # JS interop wrapper (IndexedDbJsInterop)
│   ├── Modeling/                # ModelBuilder, fluent builders, schema types
│   ├── Options/                 # IndexedDbOptions
│   ├── Query/                   # IndexedDbQuery<T>, QueryTranslator
│   ├── Reactive/                # LiveQueryObservable<T>
│   └── wwwroot/blazordb.js      # IndexedDB JS implementation
├── BlazorIdb.Sample/            # Blazor WASM sample application
├── BlazorIdb.SourceGenerators/  # Roslyn incremental source generator
└── BlazorIdb.Tests/             # xUnit unit tests
```

---

## License

[MIT](LICENSE)
