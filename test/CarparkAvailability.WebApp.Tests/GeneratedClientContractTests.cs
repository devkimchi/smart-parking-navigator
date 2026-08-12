using System.Net;
using System.Text;
using CarparkAvailability.WebApp.Generated;

namespace CarparkAvailability.WebApp.Tests;

public sealed class GeneratedClientContractTests
{
    [Fact]
    public async Task SearchDeserializesEnumCollectionsFromOpenApiWireValues()
    {
        const string json = """
            {
              "results": [
                {
                  "carParkNumber": "A1",
                  "address": "TEST STREET",
                  "coordinate": { "latitude": 1.31, "longitude": 103.81 },
                  "carParkType": "surface",
                  "sourceCarParkType": "SURFACE CAR PARK",
                  "parkingSystem": "ELECTRONIC PARKING",
                  "nightParking": true,
                  "decks": 1,
                  "gantryHeight": 2.1,
                  "basement": false,
                  "shortTermParking": "WHOLE DAY",
                  "freeParking": "NO",
                  "incompleteMetadata": true,
                  "distanceMetres": 100,
                  "availability": {
                    "lots": [],
                    "sourceUpdateTime": null,
                    "retrievalTime": null,
                    "freshness": "unavailable",
                    "occupancyRate": null
                  },
                  "compatible": false,
                  "recommended": false,
                  "exclusionReasons": ["unavailable", "incompleteMetadata"],
                  "rankFactors": null
                }
              ],
              "radiusMetres": 500
            }
            """;
        using HttpClient httpClient = new(new StubHttpMessageHandler(json))
        {
            BaseAddress = new Uri("https://apiapp/")
        };
        CarparkAvailabilityApiClient client = new(httpClient);

        CarParkSearchResponse response = await client.SearchCarParksAsync(
            1.31,
            103.81,
            VehicleType.Car,
            cancellationToken: TestContext.Current.CancellationToken);

        CarParkSearchResult result = Assert.Single(response.Results);
        Assert.Equal(
            [ExclusionReason.Unavailable, ExclusionReason.IncompleteMetadata],
            result.ExclusionReasons);
    }

    private sealed class StubHttpMessageHandler(string content) : HttpMessageHandler
    {
        private readonly string _content = content;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
