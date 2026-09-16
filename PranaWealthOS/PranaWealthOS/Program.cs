using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Charts;
using MudBlazor.Services;
using PranaWealthOS;
using PranaWealthOS.Auth;
using Supabase;

var culture = new System.Globalization.CultureInfo("en-IN");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();

// --- NEW: SUPABASE INITIALIZATION ---
var url = "https://hkxzrwutrfkboqzgcktg.supabase.co"; 
var key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImhreHpyd3V0cmZrYm9xemdja3RnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODA0NjU5NTcsImV4cCI6MjA5NjA0MTk1N30.SFHP3n_bS69j3NL0r19as1Dl1KpFhD8zZUyxTniqGhs";

var options = new SupabaseOptions
{
    AutoRefreshToken = true,
    AutoConnectRealtime = true
};

// Register Supabase as a Singleton so the entire app can access it
builder.Services.AddSingleton(provider => new Supabase.Client(url, key, options));
// ------------------------------------

// Build the app
var host = builder.Build();

// Tell Supabase to wake up and check for saved logins in the browser/URL
var supabaseClient = host.Services.GetRequiredService<Supabase.Client>();
await supabaseClient.InitializeAsync();

// Run the app
await host.RunAsync();

/*
Deployment Instructions:
dotnet publish -c Release
netlify deploy --prod --dir=wwwroot/dist -m "Fixed tab bar alignment and added floating button"
or 
upload: 
1. run the cmd: dotnet publish -c Release
2. copy past the folder, Exact Path: bin \ Release \ net8.0 \ publish \ wwwroot
*/
