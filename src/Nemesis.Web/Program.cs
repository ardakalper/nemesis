using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Nemesis.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<GameState>();
await builder.Build().RunAsync();
