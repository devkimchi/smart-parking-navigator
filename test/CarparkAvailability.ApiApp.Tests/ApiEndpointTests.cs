using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using CarparkAvailability.ApiApp.Contracts;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class ApiEndpointTests
{
    [Fact]
    public async Task AllApiOperationsReturnContractResponses()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage searchResponse = await client.GetAsync(
            "/api/carparks?latitude=1.366666&longitude=103.833333&vehicleType=car",
            cancellationToken);
        using HttpResponseMessage detailResponse = await client.GetAsync(
            "/api/carparks/abc?vehicleType=car&latitude=1.366666&longitude=103.833333",
            cancellationToken);
        DataStatusResponse? status = await client.GetFromJsonAsync<DataStatusResponse>(
            "/api/data-status",
            cancellationToken);
        using HttpResponseMessage refreshResponse = await client.PostAsync(
            "/api/data-status/refresh",
            content: null,
            cancellationToken);
        DataStatusResponse? refreshedStatus =
            await refreshResponse.Content.ReadFromJsonAsync<DataStatusResponse>(cancellationToken);
        using HttpResponseMessage openApiResponse = await client.GetAsync("/openapi.json", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        CarParkSearchResponse? search = await searchResponse.Content.ReadFromJsonAsync<CarParkSearchResponse>(
            cancellationToken);
        Assert.NotNull(search);
        Assert.Equal("ABC", Assert.Single(search.Results).CarParkNumber);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(status);
        Assert.Equal(1, status.StaticRecordCount);
        Assert.Equal(1, status.MatchedRecordCount);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshedStatus);
        Assert.Equal("fresh", refreshedStatus.Freshness);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidQueryReturnsRfc9457ValidationProblem()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.GetAsync(
            "/api/carparks?latitude=91&longitude=bad&vehicleType=truck&availableOnly=maybe",
            cancellationToken);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("latitude", out _));
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UnknownDetailReturnsRfc9457NotFoundProblem()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.GetAsync(
            "/api/carparks/UNKNOWN",
            cancellationToken);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task DetailRejectsIncompleteDestinationCoordinates()
    {
        using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using HttpResponseMessage response = await client.GetAsync(
            "/api/carparks/abc?vehicleType=car&latitude=1.366666",
            TestContext.Current.CancellationToken);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("destination", out _));
    }

    [Fact]
    public void MissingCatalogFailsApplicationStartup()
    {
        string missingPath = Path.Combine(AppContext.BaseDirectory, $"missing-{Guid.NewGuid():N}.csv");
        using ApiFactory factory = new(missingPath, ownsCatalog: false);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private static readonly DateTimeOffset Now = new(2026, 8, 12, 1, 0, 0, TimeSpan.Zero);
        private readonly string _catalogPath;
        private readonly bool _ownsCatalog;

        public ApiFactory()
        {
            _catalogPath = CatalogAndGeospatialTests.WriteCsv(
                "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement",
                "ABC,Sample address,28001.642,38744.572,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,YES,1,2.0,N");
            _ownsCatalog = true;
        }

        public ApiFactory(string catalogPath, bool ownsCatalog)
        {
            _catalogPath = catalogPath;
            _ownsCatalog = ownsCatalog;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["StaticData:CsvPath"] = _catalogPath,
                    ["Availability:PollInterval"] = "01:00:00"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAvailabilityClient>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                AvailabilitySnapshot snapshot = new(
                    Now.AddSeconds(-30),
                    Now,
                    new Dictionary<string, CarParkAvailability>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ABC"] = new CarParkAvailability(
                            "ABC",
                            Now.AddSeconds(-30),
                            [new LotAvailability(LotType.Car, 10, 4)])
                    }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
                    0);
                services.AddSingleton<IAvailabilityClient>(
                    new QueueAvailabilityClient(snapshot));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _ownsCatalog)
            {
                File.Delete(_catalogPath);
            }
        }
    }
}
