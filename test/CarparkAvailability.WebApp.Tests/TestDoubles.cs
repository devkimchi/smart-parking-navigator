using CarparkAvailability.WebApp.Generated;
using CarparkAvailability.WebApp.Services;
using Microsoft.AspNetCore.Components;

namespace CarparkAvailability.WebApp.Tests;

internal sealed class FakeApiClient : ICarparkAvailabilityApiClient
{
    public delegate Task<CarParkSearchResponse> SearchAsyncHandler(
        double latitude,
        double longitude,
        VehicleType vehicleType,
        bool? availableOnly,
        bool? nightParking,
        IEnumerable<CarParkType>? carParkTypes,
        CancellationToken cancellationToken);

    public delegate Task<CarParkDetail> DetailAsyncHandler(
        string carParkNumber,
        VehicleType? vehicleType,
        double? latitude,
        double? longitude,
        bool? nightParking,
        IEnumerable<CarParkType>? carParkTypes,
        CancellationToken cancellationToken);

    public Func<double, double, VehicleType, bool?, bool?, IEnumerable<CarParkType>?, CarParkSearchResponse> Search { get; set; } =
        static (_, _, _, _, _, _) => new CarParkSearchResponse();

    public Func<string, VehicleType?, CarParkDetail> Detail { get; set; } =
        static (number, _) => TestData.Detail(number);

    public SearchAsyncHandler? SearchAsync { get; set; }

    public DetailAsyncHandler? DetailAsync { get; set; }

    public Func<DataStatus> Status { get; set; } =
        static () => new DataStatus
        {
            Freshness = Freshness.Fresh,
            SourceUpdateTime = new DateTimeOffset(2026, 8, 12, 1, 0, 0, TimeSpan.Zero),
        };

    public Func<DataStatus> RefreshStatus { get; set; } =
        static () => new DataStatus
        {
            Freshness = Freshness.Fresh,
            SourceUpdateTime = new DateTimeOffset(2026, 8, 12, 1, 1, 0, TimeSpan.Zero),
        };

    public Func<CancellationToken, Task<DataStatus>>? RefreshStatusAsync { get; set; }

    public int RefreshCount { get; private set; }

    public Task<CarParkSearchResponse> SearchCarParksAsync(
        double latitude,
        double longitude,
        VehicleType vehicleType,
        bool? availableOnly = null,
        bool? nightParking = null,
        IEnumerable<CarParkType>? carParkType = null,
        CancellationToken cancellationToken = default)
    {
        return SearchAsync is null
            ? Task.FromResult(Search(latitude, longitude, vehicleType, availableOnly, nightParking, carParkType))
            : SearchAsync(
                latitude,
                longitude,
                vehicleType,
                availableOnly,
                nightParking,
                carParkType,
                cancellationToken);
    }

    public Task<CarParkDetail> GetCarParkAsync(
        string carParkNumber,
        VehicleType? vehicleType = null,
        double? latitude = null,
        double? longitude = null,
        bool? nightParking = null,
        IEnumerable<CarParkType>? carParkType = null,
        CancellationToken cancellationToken = default)
    {
        return DetailAsync is null
            ? Task.FromResult(Detail(carParkNumber, vehicleType))
            : DetailAsync(
                carParkNumber,
                vehicleType,
                latitude,
                longitude,
                nightParking,
                carParkType,
                cancellationToken);
    }

    public Task<DataStatus> GetDataStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Status());
    }

    public Task<DataStatus> RefreshDataStatusAsync(CancellationToken cancellationToken = default)
    {
        RefreshCount++;
        return RefreshStatusAsync is null
            ? Task.FromResult(RefreshStatus())
            : RefreshStatusAsync(cancellationToken);
    }
}

internal sealed class FakeMapInterop : IMapInterop
{
    public int LocationRequestCount { get; private set; }

    public bool Initialized { get; set; } = true;

    public GeocodeResult GeocodeResult { get; set; } = new(
        GeocodeResultStatus.Resolved,
        new MapCoordinate(1.3, 103.8),
        "Resolved destination",
        null);

    public LocationResult LocationResult { get; set; } = new(
        LocationResultStatus.Denied,
        null,
        "Location permission was denied.");

    public MapLocationLabel? ResolvedLocation { get; set; } = new(
        "Marina Bay Sands",
        "10 Bayfront Avenue, Singapore 018956");

    public List<MapMarker> Markers { get; } = [];

    public string? OriginLabel { get; private set; }

    public string? SelectedMarker { get; private set; }

    public Task<bool> InitializeAsync(
        ElementReference mapElement,
        object callbackReceiver,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Initialized);
    }

    public Task<GeocodeResult> GeocodeAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GeocodeResult);
    }

    public Task<LocationResult> RequestCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        LocationRequestCount++;
        return Task.FromResult(LocationResult);
    }

    public Task<MapLocationLabel?> ResolveLocationAsync(
        MapCoordinate coordinate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ResolvedLocation);
    }

    public Task SetMarkersAsync(
        IReadOnlyCollection<MapMarker> markers,
        MapCoordinate? origin,
        string? originLabel,
        CancellationToken cancellationToken = default)
    {
        Markers.Clear();
        Markers.AddRange(markers);
        OriginLabel = originLabel;
        return Task.CompletedTask;
    }

    public Task SelectMarkerAsync(
        string? carParkNumber,
        CancellationToken cancellationToken = default)
    {
        SelectedMarker = carParkNumber;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal static class TestData
{
    public static CarParkSearchResult SearchResult(
        string number = "A1",
        int availableLots = 12,
        Freshness freshness = Freshness.Fresh,
        bool recommended = false)
    {
        return new CarParkSearchResult
        {
            CarParkNumber = number,
            Address = $"{number} TEST STREET",
            Coordinate = new Coordinate { Latitude = 1.3, Longitude = 103.8 },
            CarParkType = CarParkType.MultiStorey,
            ParkingSystem = "Electronic",
            NightParking = true,
            DistanceMetres = 180,
            Compatible = true,
            Recommended = recommended,
            Availability = new Availability
            {
                Freshness = freshness,
                SourceUpdateTime = new DateTimeOffset(2026, 8, 12, 1, 0, 0, TimeSpan.Zero),
                OccupancyRate = 0.75,
                Lots =
                [
                    new LotAvailability
                    {
                        LotType = LotType.Car,
                        TotalLots = 48,
                        AvailableLots = availableLots,
                    },
                ],
            },
            RankFactors = new RankFactors
            {
                Score = 0.8,
                DistanceScore = 0.8,
                AvailabilityScore = 0.7,
                OccupancyScore = 0.6,
            },
        };
    }

    public static CarParkDetail Detail(
        string number = "A1",
        int availableLots = 12,
        ICollection<CarParkSearchResult>? alternatives = null)
    {
        return new CarParkDetail
        {
            CarParkNumber = number,
            Address = $"{number} TEST STREET",
            Coordinate = new Coordinate { Latitude = 1.3, Longitude = 103.8 },
            CarParkType = CarParkType.MultiStorey,
            SourceCarParkType = "MULTI-STOREY CAR PARK",
            ParkingSystem = "Electronic",
            NightParking = true,
            Decks = 5,
            GantryHeight = 2.1,
            Basement = false,
            ShortTermParking = "WHOLE DAY",
            FreeParking = "NO",
            Compatible = true,
            Availability = new Availability
            {
                Freshness = Freshness.Fresh,
                SourceUpdateTime = new DateTimeOffset(2026, 8, 12, 1, 0, 0, TimeSpan.Zero),
                Lots =
                [
                    new LotAvailability
                    {
                        LotType = LotType.Car,
                        TotalLots = 48,
                        AvailableLots = availableLots,
                    },
                    new LotAvailability
                    {
                        LotType = LotType.Motorcycle,
                        TotalLots = 10,
                        AvailableLots = 3,
                    },
                ],
            },
            Alternatives = alternatives ?? [],
        };
    }
}
