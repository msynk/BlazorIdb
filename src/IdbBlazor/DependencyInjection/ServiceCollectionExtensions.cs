using IdbBlazor.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace IdbBlazor.DependencyInjection;

/// <summary>
/// Extension methods for registering IdbBlazor services with a
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a scoped service so that
    /// Blazor components can inject it directly.
    /// </summary>
    /// <typeparam name="TContext">
    /// The application's IndexedDB context (must derive from <see cref="IndexedDbContext"/>).
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional delegate to configure the database name, version, or other options.
    /// </param>
    /// <example>
    /// <code>
    /// // Program.cs
    /// builder.Services.AddIndexedDb&lt;AppDb&gt;();
    ///
    /// // Or with explicit options:
    /// builder.Services.AddIndexedDb&lt;AppDb&gt;(opts =&gt;
    ///     opts.UseDatabase("AppDb", version: 2));
    /// </code>
    /// </example>
    public static IServiceCollection AddIndexedDb<TContext>(
        this IServiceCollection services,
        Action<IndexedDbOptions>? configure = null)
        where TContext : IndexedDbContext
    {
        var options = new IndexedDbOptions();
        configure?.Invoke(options);

        // Register options as singleton so the context constructor can receive it
        services.AddSingleton(options);

        services.AddScoped(sp =>
        {
            var js = sp.GetRequiredService<IJSRuntime>();
            var opts = sp.GetRequiredService<IndexedDbOptions>();
            return (TContext)Activator.CreateInstance(typeof(TContext), js, opts)!;
        });

        return services;
    }
}
