using IdbBlazor.DependencyInjection;
using IdbBlazor.Sample;
using IdbBlazor.Sample.Data;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Register the IdbBlazor context with explicit version
builder.Services.AddIndexedDb<AppDb>(opts =>
    opts.UseDatabase("IdbBlazorSampleApp", version: 2));

await builder.Build().RunAsync();
