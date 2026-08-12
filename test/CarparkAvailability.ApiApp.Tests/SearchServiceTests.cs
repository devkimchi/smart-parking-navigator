using System.Collections.Immutable;
using CarparkAvailability.ApiApp.Contracts;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Domain.Recommendations;
using CarparkAvailability.ApiApp.Options;
using CarparkAvailability.ApiApp.Services;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class SearchServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 1, 0, 0, TimeSpan.Zero);
    private static readonly GeoCoordinate Origin = new(1.366666, 103.833333);

    [Fact]
    public void SearchAppliesCompatibilityHardFiltersAndExcludesMissingLiveAvailability()
    {
        CarPark compatible = CreateCarPark("A", 0, CarParkType.Surface, true);
        CarPark incompatible = CreateCarPark("B", 0.0001, CarParkType.Surface, false);
        CarPark unknown = CreateCarPark("C", 0.0002, CarParkType.MultiStorey, true);
        CarParkCatalog catalog = new([compatible, incompatible, unknown], 0);
        AvailabilitySnapshotStore store = StoreWith(
            AvailabilityFor("A", new LotAvailability(LotType.Car, 10, 2)),
            AvailabilityFor("B", new LotAvailability(LotType.Motorcycle, 10, 5)));
        CarParkSearchService service = CreateService(catalog, store);

        CarParkSearchResponse response = service.Search(new SearchCriteria(
            Origin,
            VehicleType.Car,
            false,
            null,
            new HashSet<CarParkType>()));

        Assert.Equal(2, response.Results.Count);
        Assert.True(response.Results[0].Recommended);
        CarParkSearchResultResponse incompatibleResult = Assert.Single(
            response.Results,
            static result => result.CarParkNumber == "B");
        Assert.Contains("incompatibleVehicleType", incompatibleResult.ExclusionReasons);
        Assert.False(incompatibleResult.Recommended);
        Assert.DoesNotContain(response.Results, static result => result.CarParkNumber == "C");

        CarParkSearchResponse filtered = service.Search(new SearchCriteria(
            Origin,
            VehicleType.Car,
            true,
            true,
            new HashSet<CarParkType> { CarParkType.Surface }));
        Assert.Single(filtered.Results);
        Assert.Equal("A", filtered.Results[0].CarParkNumber);
    }

    [Fact]
    public void RankingUsesWeightsAndStableIdentifierTieBreak()
    {
        CarPark a = CreateCarPark("A", 0.001, CarParkType.Surface, true);
        CarPark b = CreateCarPark("B", 0.001, CarParkType.Surface, true);
        CarPark fartherWithLots = CreateCarPark("C", 0.002, CarParkType.Surface, true);
        CarParkCatalog catalog = new([b, fartherWithLots, a], 0);
        AvailabilitySnapshotStore store = StoreWith(
            AvailabilityFor("A", new LotAvailability(LotType.Car, 10, 5)),
            AvailabilityFor("B", new LotAvailability(LotType.Car, 10, 5)),
            AvailabilityFor("C", new LotAvailability(LotType.Car, 20, 20)));
        CarParkSearchService service = CreateService(catalog, store);

        CarParkSearchResponse response = service.Search(DefaultCriteria());

        Assert.Equal("C", response.Results[0].CarParkNumber);
        Assert.Equal("A", response.Results[1].CarParkNumber);
        Assert.Equal("B", response.Results[2].CarParkNumber);
        Assert.NotNull(response.Results[0].RankFactors);
        Assert.True(response.Results[0].RankFactors!.AvailabilityScore > response.Results[1].RankFactors!.AvailabilityScore);
    }

    [Fact]
    public void FreshResultBreaksOtherwiseEquivalentStaleTie()
    {
        CarPark fresh = CreateCarPark("FRESH", 0.001, CarParkType.Surface, true);
        CarPark stale = CreateCarPark("STALE", 0.001, CarParkType.Surface, true);
        CarParkCatalog catalog = new([stale, fresh], 0);
        AvailabilitySnapshotStore store = new();
        AvailabilitySnapshot snapshot = new(
            Now.AddSeconds(-30),
            Now,
            new[]
            {
                new CarParkAvailability(
                    "FRESH",
                    Now.AddSeconds(-30),
                    [new LotAvailability(LotType.Car, 10, 5)]),
                new CarParkAvailability(
                    "STALE",
                    Now.AddMinutes(-5),
                    [new LotAvailability(LotType.Car, 10, 5)])
            }.ToImmutableDictionary(
                static record => record.CarParkNumber,
                StringComparer.OrdinalIgnoreCase),
            0);
        store.RecordSuccess(snapshot, Now);

        CarParkSearchResponse response = CreateService(catalog, store).Search(DefaultCriteria());

        Assert.Equal("FRESH", response.Results[0].CarParkNumber);
        Assert.Equal("STALE", response.Results[1].CarParkNumber);
        Assert.Equal("fresh", response.Results[0].Availability.Freshness);
        Assert.Equal("stale", response.Results[1].Availability.Freshness);
    }

    [Fact]
    public void SearchIncludesExactRadiusAndExcludesBeyondRadius()
    {
        double boundaryDelta = 500 / Infrastructure.Geospatial.GeospatialDistance.EarthRadiusMetres * 180 / Math.PI;
        CarPark boundary = CreateCarPark("BOUNDARY", boundaryDelta, CarParkType.Surface, true);
        CarPark outside = CreateCarPark("OUTSIDE", boundaryDelta * 1.001, CarParkType.Surface, true);
        CarParkCatalog catalog = new([boundary, outside], 0);
        AvailabilitySnapshotStore store = StoreWith(
            AvailabilityFor("BOUNDARY", new LotAvailability(LotType.Car, 10, 1)),
            AvailabilityFor("OUTSIDE", new LotAvailability(LotType.Car, 10, 1)));

        CarParkSearchResponse response = CreateService(catalog, store).Search(DefaultCriteria());

        Assert.Single(response.Results);
        Assert.Equal("BOUNDARY", response.Results[0].CarParkNumber);
    }

    [Fact]
    public void FullDetailReturnsVerifiedAlternativesAndExcludesSelected()
    {
        CarPark selected = CreateCarPark("FULL", 0, CarParkType.Surface, true);
        CarPark alternative = CreateCarPark("OPEN", 0.001, CarParkType.MultiStorey, true);
        CarParkCatalog catalog = new([selected, alternative], 0);
        AvailabilitySnapshotStore store = StoreWith(
            AvailabilityFor("FULL", new LotAvailability(LotType.Car, 10, 0)),
            AvailabilityFor("OPEN", new LotAvailability(LotType.Car, 10, 3)));
        CarParkSearchService service = CreateService(catalog, store);

        CarParkDetailResponse detail = Assert.IsType<CarParkDetailResponse>(
            service.GetDetail(" full ", VehicleType.Car, DefaultCriteria()));

        Assert.Contains("full", detail.ExclusionReasons);
        CarParkSearchResultResponse result = Assert.Single(detail.Alternatives);
        Assert.Equal("OPEN", result.CarParkNumber);
        Assert.True(result.Recommended);
    }

    [Fact]
    public void FullDetailKeepsDestinationAndActiveFiltersForAlternatives()
    {
        CarPark selected = CreateCarPark("FULL", 0.001, CarParkType.MultiStorey, true);
        CarPark valid = CreateCarPark("VALID", 0.002, CarParkType.MultiStorey, true);
        CarPark wrongType = CreateCarPark("SURFACE", 0.0021, CarParkType.Surface, true);
        CarPark noNightParking = CreateCarPark("DAY", 0.0022, CarParkType.MultiStorey, false);
        CarParkCatalog catalog = new([selected, valid, wrongType, noNightParking], 0);
        AvailabilitySnapshotStore store = StoreWith(
            AvailabilityFor("FULL", new LotAvailability(LotType.Car, 10, 0)),
            AvailabilityFor("VALID", new LotAvailability(LotType.Car, 10, 3)),
            AvailabilityFor("SURFACE", new LotAvailability(LotType.Car, 10, 4)),
            AvailabilityFor("DAY", new LotAvailability(LotType.Car, 10, 5)));
        SearchCriteria criteria = new(
            Origin,
            VehicleType.Car,
            false,
            true,
            new HashSet<CarParkType> { CarParkType.MultiStorey });

        CarParkDetailResponse detail = Assert.IsType<CarParkDetailResponse>(
            CreateService(catalog, store).GetDetail("FULL", VehicleType.Car, criteria));

        CarParkSearchResultResponse alternative = Assert.Single(detail.Alternatives);
        Assert.Equal("VALID", alternative.CarParkNumber);
        Assert.InRange(alternative.DistanceMetres, 220, 224);
    }

    private static SearchCriteria DefaultCriteria()
    {
        return new SearchCriteria(
            Origin,
            VehicleType.Car,
            false,
            null,
            new HashSet<CarParkType>());
    }

    private static CarPark CreateCarPark(
        string identifier,
        double latitudeDelta,
        CarParkType type,
        bool nightParking)
    {
        return new CarPark(
            identifier,
            $"{identifier} address",
            new GeoCoordinate(Origin.Latitude + latitudeDelta, Origin.Longitude),
            type,
            type.ToString(),
            "ELECTRONIC",
            "WHOLE DAY",
            "NO",
            nightParking,
            1,
            2,
            false,
            false);
    }

    private static CarParkAvailability AvailabilityFor(
        string identifier,
        params LotAvailability[] lots)
    {
        return new CarParkAvailability(identifier, Now.AddSeconds(-30), [.. lots]);
    }

    private static AvailabilitySnapshotStore StoreWith(params CarParkAvailability[] records)
    {
        AvailabilitySnapshotStore store = new();
        AvailabilitySnapshot snapshot = new(
            Now.AddSeconds(-30),
            Now,
            records.ToImmutableDictionary(
                static record => record.CarParkNumber,
                StringComparer.OrdinalIgnoreCase),
            0);
        store.RecordAttempt(Now);
        store.RecordSuccess(snapshot, Now);
        return store;
    }

    private static CarParkSearchService CreateService(
        CarParkCatalog catalog,
        AvailabilitySnapshotStore store)
    {
        return new CarParkSearchService(
            catalog,
            store,
            Microsoft.Extensions.Options.Options.Create(new SearchOptions()),
            Microsoft.Extensions.Options.Options.Create(new AvailabilityOptions()),
            new FixedTimeProvider(Now));
    }
}
