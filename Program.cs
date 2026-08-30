using Kidz2Learn;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddMudServices();
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<SidPlayerService>();
builder.Services.AddSingleton<LoggerService>();
builder.Services.AddSingleton<ScoreService>();
builder.Services.AddSingleton<SidWidgetService>();
builder.Services.AddSingleton<HudStateService>();
builder.Services.AddScoped<AffirmationService>();
builder.Services.AddScoped<TaskSessionController>();

builder.Services.AddIndexedDbService();
// all options
builder.Services.AddIndexedDb(
    "AufgabenDB", // the database name
    ["ArithmetikAufgaben", "LeseAufgaben", "SkillMeta", "SkillStates", "SilbenHammerRatings"], // the names of value stores
    4); // the version number of the current database schema

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();