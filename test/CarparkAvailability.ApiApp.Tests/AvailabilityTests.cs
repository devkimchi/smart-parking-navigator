using System.Text.Json;
using System.Text;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated;
using CarparkAvailability.ApiApp.Options;
using CarparkAvailability.ApiApp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class AvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 1, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("C", LotType.Car)]
    [InlineData("H", LotType.HeavyVehicle)]
    [InlineData("Y", LotType.Motorcycle)]
    [InlineData("X", LotType.Other)]
    public void LotTypeMappingUsesOfficialCodes(string code, LotType expected)
    {
        Assert.Equal(expected, LotTypeMapper.Map(code));
    }

    [Fact]
    public void OccupancyAndFreshnessDoNotInventUnknownValues()
    {
        LotAvailability availability = new(LotType.Car, 10, 3);
        LotAvailability zeroCapacity = new(LotType.Car, 0, 0);

        Assert.Equal(0.7, availability.OccupancyRate);
        Assert.Null(zeroCapacity.OccupancyRate);
        Assert.Equal(
            FreshnessState.Fresh,
            FreshnessCalculator.Calculate(Now.AddSeconds(-120), Now, TimeSpan.FromSeconds(120)));
        Assert.Equal(
            FreshnessState.Stale,
            FreshnessCalculator.Calculate(Now.AddSeconds(-121), Now, TimeSpan.FromSeconds(120)));
        Assert.Equal(
            FreshnessState.Unavailable,
            FreshnessCalculator.Calculate(null, Now, TimeSpan.FromSeconds(120)));
    }

    [Fact]
    public void ParserAcceptsOfficialShapeAndUnknownOptionalFields()
    {
        const string json = """
            {
              "items": [{
                "timestamp": "2026-08-12T09:00:00+08:00",
                "carpark_data": [{
                  "carpark_number": "ABC",
                  "update_datetime": "2026-08-12T09:00:00+08:00",
                  "unknown": { "safe": true },
                  "carpark_info": [
                    { "total_lots": "20", "lot_type": "C", "lots_available": "5", "extra": 1 },
                    { "total_lots": "4", "lot_type": "Z", "lots_available": "2" }
                  ]
                }]
              }],
              "api_info": { "status": "healthy" }
            }
            """;
        DataGovSgAvailabilityClient client = CreateParser();

        AvailabilitySnapshot snapshot = client.Parse(DeserializeResponse(json), Now);

        CarParkAvailability record = Assert.Single(snapshot.Records).Value;
        Assert.Equal("ABC", record.CarParkNumber);
        Assert.Equal(2, record.Lots.Length);
        Assert.Contains(record.Lots, static lot => lot.LotType == LotType.Car);
        Assert.Contains(record.Lots, static lot => lot.LotType == LotType.Other);
        Assert.Equal(0, snapshot.ValidationErrorCount);
    }

    [Fact]
    public void ParserQuarantinesImpossibleCountsInDocumentedShape()
    {
        const string json = """
            {
              "items": [{
                "timestamp": "2026-08-12T09:00:00+08:00",
                "carpark_data": [{
                  "carpark_number": "ABC",
                  "update_datetime": "2026-08-12T09:00:00+08:00",
                  "carpark_info": [
                    { "total_lots": "10", "lot_type": "C", "lots_available": "11" },
                    { "total_lots": "5", "lot_type": "Y", "lots_available": "2" }
                  ]
                }]
              }]
            }
            """;
        DataGovSgAvailabilityClient client = CreateParser();

        AvailabilitySnapshot snapshot = client.Parse(DeserializeResponse(json), Now);

        Assert.Equal(1, snapshot.ValidationErrorCount);
        LotAvailability lot = Assert.Single(Assert.Single(snapshot.Records).Value.Lots);
        Assert.Equal(LotType.Motorcycle, lot.LotType);
    }

    [Fact]
    public void ParserTreatsOffsetlessSourceTimesAsSingaporeTime()
    {
        const string json = """
            {
              "items": [{
                "carpark_data": [{
                  "carpark_number": "ABC",
                  "update_datetime": "2026-08-12T09:00:00",
                  "carpark_info": [
                    { "total_lots": "5", "lot_type": "C", "lots_available": "2" }
                  ]
                }]
              }]
            }
            """;
        DataGovSgAvailabilityClient client = CreateParser();

        AvailabilitySnapshot snapshot = client.Parse(DeserializeResponse(json), Now);

        Assert.Equal(Now, Assert.Single(snapshot.Records).Value.UpdateTime);
        Assert.Equal(Now, snapshot.SourceUpdateTime);
    }

    [Fact]
    public async Task FetchUsesHttpsAndConfiguredServerSideApiKey()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const string json = """
            {"items":[{"carpark_data":[{
              "carpark_number":"ABC",
              "update_datetime":"2026-08-12T09:00:00+08:00",
              "carpark_info":[{"total_lots":"5","lot_type":"C","lots_available":"2"}]
            }]}]}
            """;
        StubHttpMessageHandler handler = new((request, _) =>
        {
            Assert.Equal(Uri.UriSchemeHttps, request.RequestUri?.Scheme);
            Assert.Equal(
                new Uri("https://api.data.gov.sg/v1/transport/carpark-availability"),
                request.RequestUri);
            Assert.Equal("server-secret", Assert.Single(request.Headers.GetValues("x-api-key")));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        AvailabilityOptions options = new()
        {
            ApiKey = "server-secret"
        };
        DataGovSgAvailabilityClient client = CreateClient(handler, options);

        AvailabilitySnapshot snapshot = await client.FetchAsync(cancellationToken);

        Assert.Single(snapshot.Records);
    }

    [Fact]
    public async Task FetchSurfacesUpstreamThrottling()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        StubHttpMessageHandler handler = new(static (_, _) =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    """{"code":429,"message":"Too many requests"}""",
                    Encoding.UTF8,
                    "application/json")
            });
        DataGovSgAvailabilityClient client = CreateClient(handler);

        await Assert.ThrowsAsync<ApiException<Error>>(
            () => client.FetchAsync(cancellationToken));
    }

    [Theory]
    [InlineData("""{"items":[{"carpark_data":[{"carpark_number":"ABC","carpark_info":[]}]}]}""")]
    [InlineData("""{"items":[]}""")]
    [InlineData("""{"items":null}""")]
    public void ParserRejectsMissingOrWrongRequiredFields(string json)
    {
        DataGovSgAvailabilityClient client = CreateParser();

        Assert.Throws<InvalidDataException>(() => client.Parse(DeserializeResponse(json), Now));
    }

    [Fact]
    public async Task GeneratedClientRejectsContractTypeMismatch()
    {
        const string json = """
            {"items":[{"carpark_data":[{
              "carpark_number":"ABC",
              "update_datetime":"2026-08-12T09:00:00+08:00",
              "carpark_info":[{"total_lots":5,"lot_type":"C","lots_available":"2"}]
            }]}]}
            """;
        StubHttpMessageHandler handler = new(static (_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        DataGovSgAvailabilityClient client = CreateClient(handler);

        await Assert.ThrowsAsync<ApiException>(
            () => client.FetchAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefreshRetainsLastKnownGoodSnapshotAfterFailure()
    {
        AvailabilitySnapshot valid = QueueAvailabilityClient.Snapshot(
            Now,
            Now,
            new CarParkAvailability(
                "ABC",
                Now,
                [new LotAvailability(LotType.Car, 10, 2)]));
        ApiException upstreamFailure = new(
            "Upstream throttled the request.",
            (int)HttpStatusCode.TooManyRequests,
            """{"code":429,"message":"Too many requests"}""",
            new Dictionary<string, IEnumerable<string>>(),
            null);
        QueueAvailabilityClient client = new(valid, upstreamFailure);
        AvailabilitySnapshotStore store = new();
        AvailabilityRefreshService service = new(
            client,
            store,
            Microsoft.Extensions.Options.Options.Create(new AvailabilityOptions()),
            new FixedTimeProvider(Now),
            NullLogger<AvailabilityRefreshService>.Instance);

        Assert.True(await service.RefreshOnceAsync(CancellationToken.None));
        Assert.False(await service.RefreshOnceAsync(CancellationToken.None));

        AvailabilityStatus status = store.GetStatus();
        Assert.Same(valid, status.Snapshot);
        Assert.Contains("Upstream throttled the request.", status.LastError);
        Assert.Equal(Now, status.LastSuccessTime);
    }

    [Fact]
    public async Task ManualRefreshDoesNotExceedConfiguredPollFrequency()
    {
        AvailabilitySnapshot valid = QueueAvailabilityClient.Snapshot(
            Now,
            Now,
            new CarParkAvailability(
                "ABC",
                Now,
                [new LotAvailability(LotType.Car, 10, 2)]));
        QueueAvailabilityClient client = new(
            valid,
            new InvalidOperationException("A second fetch should not occur."));
        AvailabilityRefreshService service = new(
            client,
            new AvailabilitySnapshotStore(),
            Microsoft.Extensions.Options.Options.Create(new AvailabilityOptions()),
            new FixedTimeProvider(Now),
            NullLogger<AvailabilityRefreshService>.Instance);

        Assert.True(await service.RefreshOnceAsync(CancellationToken.None));
        Assert.True(await service.RefreshIfDueAsync(CancellationToken.None));
        Assert.Equal(1, client.FetchCount);
    }

    private static DataGovSgAvailabilityClient CreateParser()
    {
        return CreateClient(new StubHttpMessageHandler(static (_, _) =>
            throw new InvalidOperationException("Parser test must not make an HTTP request.")));
    }

    private static DataGovSgAvailabilityClient CreateClient(
        HttpMessageHandler handler,
        AvailabilityOptions? options = null)
    {
        AvailabilityOptions resolvedOptions = options ?? new AvailabilityOptions();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = resolvedOptions.BaseUrl
        };
        return new DataGovSgAvailabilityClient(
            new DataGovSgApiClient(httpClient),
            Microsoft.Extensions.Options.Options.Create(resolvedOptions),
            new FixedTimeProvider(Now),
            NullLogger<DataGovSgAvailabilityClient>.Instance);
    }

    private static Response DeserializeResponse(string json)
    {
        return JsonSerializer.Deserialize<Response>(json)
            ?? throw new InvalidDataException("The test response could not be deserialized.");
    }
}
