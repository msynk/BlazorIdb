using BlazorIdb.DependencyInjection;
using BlazorIdb.Sample;
using BlazorIdb.Sample.Data;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Register the BlazorIdb context with explicit version
builder.Services.AddIndexedDb<AppDb>(opts =>
    opts.UseDatabase("BlazorIdbSampleApp", version: 2));

await builder.Build().RunAsync();
