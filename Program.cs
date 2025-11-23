using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Kidz2Learn;
using Kidz2Learn.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddMudServices();
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<SidPlayerService>();
builder.Services.AddSingleton<LoggerService>();
builder.Services.AddSingleton<ScoreService>();
builder.Services.AddSingleton<SidWidgetService>();
builder.Services.AddSingleton<HUDStateService>();

builder.Services.AddIndexedDbService();
// all options
builder.Services.AddIndexedDb(
    databaseName: "AufgabenDB", // the database name
    objectStores: ["ArithmetikAufgaben"], // the names of value stores
    version: 1); // the version number of the current database schema 

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
