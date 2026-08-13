using CarparkAvailability.WebApp.Components;
using CarparkAvailability.WebApp.Generated;
using CarparkAvailability.WebApp.Services;
using CarparkAvailability.WebApp.State;
using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
builder.Services.AddScoped<IMapInterop, GoogleMapsInterop>();
builder.Services.AddScoped<ParkingSearchState>();
builder.Services.AddHttpClient<ICarparkAvailabilityApiClient, CarparkAvailabilityApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://apiapp");
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
