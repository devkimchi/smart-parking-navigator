using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Endpoints;
using CarparkAvailability.ApiApp.Infrastructure.Csv;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated;
using CarparkAvailability.ApiApp.Infrastructure.Geospatial;
using CarparkAvailability.ApiApp.Options;
using CarparkAvailability.ApiApp.Services;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<StaticDataOptions>()
    .Bind(builder.Configuration.GetSection(StaticDataOptions.SectionName))
    .Validate(static options => !string.IsNullOrWhiteSpace(options.CsvPath), "Static CSV path is required.")
    .ValidateOnStart();
builder.Services
    .AddOptions<AvailabilityOptions>()
    .Bind(builder.Configuration.GetSection(AvailabilityOptions.SectionName))
    .Validate(
        static options => options.BaseUrl.IsAbsoluteUri &&
            options.BaseUrl.Scheme == Uri.UriSchemeHttps &&
            options.BaseUrl.AbsolutePath.EndsWith("/", StringComparison.Ordinal),
        "Availability base URL must be an absolute HTTPS URL ending with '/'.")
    .Validate(
        static options => options.PollInterval > TimeSpan.Zero &&
            options.FreshnessThreshold > TimeSpan.Zero &&
            options.RequestTimeout > TimeSpan.Zero,
        "Availability intervals and timeout must be positive.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SearchOptions>()
    .Bind(builder.Configuration.GetSection(SearchOptions.SectionName))
    .Validate(
        static options => options.RadiusMetres > 0 &&
            options.AvailableLotCap > 0 &&
            options.DistanceWeight >= 0 &&
            options.AvailabilityWeight >= 0 &&
            options.OccupancyWeight >= 0 &&
            Math.Abs(
                options.DistanceWeight +
                options.AvailabilityWeight +
                options.OccupancyWeight - 1) < 0.000001,
        "Search radius, available-lot cap, and normalized non-negative ranking weights are required.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Svy21CoordinateConverter>();
builder.Services.AddSingleton<CarParkCatalogLoader>();
builder.Services.AddSingleton(static serviceProvider =>
{
    StaticDataOptions options = serviceProvider.GetRequiredService<IOptions<StaticDataOptions>>().Value;
    string configuredPath = options.CsvPath;
    string path = Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.Combine(AppContext.BaseDirectory, configuredPath);
    return serviceProvider.GetRequiredService<CarParkCatalogLoader>().Load(path);
});
builder.Services.AddSingleton<AvailabilitySnapshotStore>();
builder.Services.AddSingleton<CarParkSearchService>();
builder.Services.AddSingleton<DataStatusService>();
builder.Services.AddHostedService<CatalogInitializationService>();
builder.Services.AddHttpClient<IDataGovSgApiClient, DataGovSgApiClient>(
    static (serviceProvider, client) =>
    {
        AvailabilityOptions options = serviceProvider.GetRequiredService<IOptions<AvailabilityOptions>>().Value;
        client.BaseAddress = options.BaseUrl;
        client.Timeout = options.RequestTimeout;
    });
builder.Services.AddTransient<IAvailabilityClient, DataGovSgAvailabilityClient>();
builder.Services.AddSingleton<AvailabilityRefreshService>();
builder.Services.AddHostedService(
    static serviceProvider => serviceProvider.GetRequiredService<AvailabilityRefreshService>());

WebApplication app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsProduction())
{
    app.MapOpenApi("/openapi.json");
}

app.UseHttpsRedirection();
app.MapCarParkApi();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
