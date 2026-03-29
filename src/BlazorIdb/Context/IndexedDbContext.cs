using System.Reflection;
using System.Text.Json;
using BlazorIdb.Annotations;
using BlazorIdb.Context;
using BlazorIdb.Interop;
using BlazorIdb.Modeling;
using BlazorIdb.Options;
using Microsoft.JSInterop;

namespace BlazorIdb;

/// <summary>
/// Base class for an application IndexedDB context.  Derive from this class,
/// declare <see cref="IndexedDbSet{T}"/> properties, and override
/// <see cref="OnModelCreating"/> for schema configuration.
/// </summary>
/// <remarks>
/// Add <c>&lt;script src="_content/BlazorIdb/blazordb.js"&gt;&lt;/script&gt;</c>
/// to your Blazor host page before using any context instance.
/// </remarks>
public abstract class IndexedDbContext : IAsyncDisposable
{
    private readonly IndexedDbJsInterop _js;
    private readonly IndexedDbOptions _options;
    private IndexedDbModel? _model;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ---- Construction ----

    /// <summary>Initializes a new context with convention-based database name and version 1.</summary>
    protected IndexedDbContext(IJSRuntime jsRuntime)
        : this(jsRuntime, new IndexedDbOptions()) { }

    /// <summary>Initializes a new context with explicit options.</summary>
    protected IndexedDbContext(IJSRuntime jsRuntime, IndexedDbOptions options)
    {
        _js = new IndexedDbJsInterop(jsRuntime);
        _options = options;
        InitializeSets();
    }

    // ---- Virtual configuration ----

    /// <summary>Gets the IndexedDB database name (defaults to the class name).</summary>
    protected virtual string DatabaseName => _options.DatabaseName ?? GetType().Name;

    /// <summary>Gets the target database version (defaults to 1).</summary>
    protected virtual int DatabaseVersion => _options.Version ?? 1;

    /// <summary>Override to configure the schema using the fluent ModelBuilder API.</summary>
    protected virtual void OnModelCreating(ModelBuilder builder) { }

    // ---- Public API ----

    /// <summary>Returns the <see cref="IndexedDbSet{T}"/> for entity type <typeparamref name="T"/>.</summary>
    public IndexedDbSet<T> Set<T>() where T : class
    {
        foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType == typeof(IndexedDbSet<T>))
                return (IndexedDbSet<T>)prop.GetValue(this)!;
        }
        return new IndexedDbSet<T>(this);
    }

    /// <summary>
    /// Executes <paramref name="action"/> within a single atomic IndexedDB transaction.
    /// All operations enqueued via the <see cref="IndexedDbTransactionContext"/> are
    /// committed together when the delegate returns.
    /// </summary>
    public async Task TransactionAsync(
        Func<IndexedDbTransactionContext, Task> action,
        string mode = "readwrite")
    {
        await EnsureInitializedAsync();
        var model = _model!;

        var tx = new IndexedDbTransactionContext(t => model.GetStoreName(t));
        await action(tx);

        if (tx.Operations.Count == 0) return;

        await _js.ExecuteTransactionAsync(
            DatabaseName,
            tx.StoreNames,
            mode,
            tx.Operations);
    }

    /// <summary>
    /// Ensures the database is open and the schema is up to date.
    /// Safe to call multiple times; initialization runs at most once per instance.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var builder = new ModelBuilder();
            ScanAnnotationsFromSets(builder);
            OnModelCreating(builder);
            _model = builder.Build(DatabaseVersion);

            var schemaJson = JsSchemaSerializer.Serialize(_model);
            var (oldVersion, newVersion) = await _js.OpenDatabaseAsync(
                DatabaseName, DatabaseVersion, schemaJson);

            if (oldVersion < newVersion)
                await RunDataMigrationsAsync(oldVersion, newVersion);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ---- Internal surface (used by IndexedDbSet<T>) ----

    internal IndexedDbJsInterop JsInterop => _js;
    internal string DbName => DatabaseName;
    internal IndexedDbModel Model
        => _model ?? throw new InvalidOperationException(
            "IndexedDbContext has not been initialized. " +
            "Await EnsureInitializedAsync() first.");

    // ---- Private helpers ----

    private void InitializeSets()
    {
        foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var t = prop.PropertyType;
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(IndexedDbSet<>)) continue;
            if (prop.GetValue(this) != null) continue;
            var entityType = t.GetGenericArguments()[0];
            var set = Activator.CreateInstance(
                typeof(IndexedDbSet<>).MakeGenericType(entityType),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null, new object[] { this }, null)!;
            prop.SetValue(this, set);
        }
    }

    private void ScanAnnotationsFromSets(ModelBuilder builder)
    {
        foreach (var prop in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var t = prop.PropertyType;
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(IndexedDbSet<>)) continue;
            var entityType = t.GetGenericArguments()[0];
            var def = AnnotationScanner.ScanType(entityType);
            if (def != null)
                builder.InjectAnnotatedStore(def);
        }
    }

    private async Task RunDataMigrationsAsync(int oldVersion, int newVersion)
    {
        foreach (var vDef in _model!.Versions)
        {
            if (vDef.Version <= oldVersion || vDef.Version > newVersion) continue;
            foreach (var (entityType, action) in vDef.DataMigrations)
            {
                var storeName = _model.GetStoreName(entityType);
                var store = new MigrationStore(_js, DatabaseName, storeName);
                await action(store);
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
