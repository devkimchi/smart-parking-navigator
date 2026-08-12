using CarparkAvailability.ApiApp.Contracts;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Domain.Recommendations;
using CarparkAvailability.ApiApp.Infrastructure.Geospatial;
using CarparkAvailability.ApiApp.Options;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Services;

public sealed class CarParkSearchService(
    CarParkCatalog catalog,
    AvailabilitySnapshotStore availabilityStore,
    IOptions<SearchOptions> searchOptions,
    IOptions<AvailabilityOptions> availabilityOptions,
    TimeProvider timeProvider)
{
    private readonly SearchOptions _searchOptions = searchOptions.Value;
    private readonly TimeSpan _freshnessThreshold = availabilityOptions.Value.FreshnessThreshold;

    public CarParkSearchResponse Search(SearchCriteria criteria)
    {
        AvailabilitySnapshot? snapshot = availabilityStore.GetStatus().Snapshot;
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<(CarParkSearchResultResponse Result, int AvailableLots)> results = [];

        foreach (CarPark carPark in catalog.Records)
        {
            double distance = GeospatialDistance.HaversineMetres(criteria.Origin, carPark.Coordinate);
            if (distance > _searchOptions.RadiusMetres + 0.000001 ||
                criteria.NightParking is not null && carPark.NightParking != criteria.NightParking.Value ||
                criteria.CarParkTypes.Count > 0 && !criteria.CarParkTypes.Contains(carPark.Type))
            {
                continue;
            }

            CarParkAvailability? liveRecord = null;
            snapshot?.Records.TryGetValue(carPark.CarParkNumber, out liveRecord);
            if (liveRecord is null)
            {
                continue;
            }

            LotType selectedLotType = MapVehicleType(criteria.VehicleType);
            LotAvailability? selectedAvailability = liveRecord.Lots.FirstOrDefault(lot => lot.LotType == selectedLotType);

            if (criteria.AvailableOnly && (selectedAvailability is null || selectedAvailability.AvailableLots <= 0))
            {
                continue;
            }

            CarParkSearchResultResponse result = CreateResult(
                carPark,
                distance,
                liveRecord,
                snapshot,
                selectedAvailability,
                now);
            results.Add((result, selectedAvailability?.AvailableLots ?? -1));
        }

        List<CarParkSearchResultResponse> sortedResults = results
            .OrderByDescending(static item => item.Result.Recommended)
            .ThenByDescending(static item => item.Result.RankFactors?.Score ?? double.MinValue)
            .ThenByDescending(static item => FreshnessSortValue(item.Result.Availability.Freshness))
            .ThenBy(static item => item.Result.DistanceMetres)
            .ThenByDescending(static item => item.AvailableLots)
            .ThenBy(static item => item.Result.CarParkNumber, StringComparer.Ordinal)
            .Select(static item => item.Result)
            .ToList();

        return new CarParkSearchResponse(sortedResults, _searchOptions.RadiusMetres);
    }

    public CarParkDetailResponse? GetDetail(
        string carParkNumber,
        VehicleType vehicleType,
        SearchCriteria? alternativeCriteria = null)
    {
        if (!catalog.TryGet(carParkNumber, out CarPark? carPark) || carPark is null)
        {
            return null;
        }

        AvailabilitySnapshot? snapshot = availabilityStore.GetStatus().Snapshot;
        CarParkAvailability? liveRecord = null;
        snapshot?.Records.TryGetValue(carPark.CarParkNumber, out liveRecord);
        LotAvailability? selectedAvailability = liveRecord?.Lots.FirstOrDefault(
            lot => lot.LotType == MapVehicleType(vehicleType));
        CarParkSearchResultResponse selected = CreateResult(
            carPark,
            0,
            liveRecord,
            snapshot,
            selectedAvailability,
            timeProvider.GetUtcNow());

        IReadOnlyList<CarParkSearchResultResponse> alternatives = [];
        if (selectedAvailability?.AvailableLots == 0 && alternativeCriteria is not null)
        {
            SearchCriteria availableAlternatives = alternativeCriteria with
            {
                VehicleType = vehicleType,
                AvailableOnly = true
            };
            alternatives = Search(availableAlternatives).Results
                .Where(result => !string.Equals(
                    result.CarParkNumber,
                    carPark.CarParkNumber,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        return new CarParkDetailResponse(
            selected.CarParkNumber,
            selected.Address,
            selected.Coordinate,
            selected.CarParkType,
            selected.SourceCarParkType,
            selected.ParkingSystem,
            selected.NightParking,
            selected.Decks,
            selected.GantryHeight,
            selected.Basement,
            selected.ShortTermParking,
            selected.FreeParking,
            selected.IncompleteMetadata,
            selected.Availability,
            selected.Compatible,
            selected.ExclusionReasons,
            alternatives);
    }

    private CarParkSearchResultResponse CreateResult(
        CarPark carPark,
        double distance,
        CarParkAvailability? liveRecord,
        AvailabilitySnapshot? snapshot,
        LotAvailability? selectedAvailability,
        DateTimeOffset now)
    {
        FreshnessState freshness = FreshnessCalculator.Calculate(
            liveRecord?.UpdateTime,
            now,
            _freshnessThreshold);
        bool incomplete = carPark.IncompleteMetadata || liveRecord is null;
        List<string> exclusionReasons = [];

        if (liveRecord is null)
        {
            exclusionReasons.Add("unavailable");
            exclusionReasons.Add("incompleteMetadata");
        }
        else if (selectedAvailability is null)
        {
            exclusionReasons.Add("incompatibleVehicleType");
        }
        else if (selectedAvailability.AvailableLots == 0)
        {
            exclusionReasons.Add("full");
        }

        double? occupancy = selectedAvailability?.OccupancyRate;
        bool compatible = selectedAvailability is not null;
        bool recommended = selectedAvailability is not null &&
            selectedAvailability.AvailableLots > 0 &&
            occupancy.HasValue &&
            freshness != FreshnessState.Unavailable;
        RankFactorsResponse? rankFactors = null;
        if (recommended && selectedAvailability is not null && occupancy.HasValue)
        {
            rankFactors = CalculateRankFactors(distance, selectedAvailability, occupancy.Value);
        }

        IReadOnlyList<LotAvailabilityResponse> lots = liveRecord?.Lots
            .Select(static lot => new LotAvailabilityResponse(
                ToCamelCase(lot.LotType),
                lot.TotalLots,
                lot.AvailableLots))
            .ToArray() ?? [];

        AvailabilityResponse availability = new(
            lots,
            liveRecord?.UpdateTime,
            snapshot?.RetrievalTime,
            ToCamelCase(freshness),
            occupancy);

        return new CarParkSearchResultResponse(
            carPark.CarParkNumber,
            carPark.Address,
            new CoordinateResponse(carPark.Coordinate.Latitude, carPark.Coordinate.Longitude),
            ToCamelCase(carPark.Type),
            carPark.SourceCarParkType,
            carPark.ParkingSystem,
            carPark.NightParking,
            carPark.CarParkDecks,
            carPark.GantryHeight,
            carPark.Basement,
            carPark.ShortTermParking,
            carPark.FreeParking,
            incomplete,
            distance,
            availability,
            compatible,
            recommended,
            exclusionReasons,
            rankFactors);
    }

    private RankFactorsResponse CalculateRankFactors(
        double distance,
        LotAvailability availability,
        double occupancy)
    {
        double distanceScore = 1 - Math.Clamp(distance / _searchOptions.RadiusMetres, 0, 1);
        double availabilityScore = Math.Clamp(
            (double)availability.AvailableLots / _searchOptions.AvailableLotCap,
            0,
            1);
        double occupancyScore = 1 - occupancy;
        double score = (_searchOptions.DistanceWeight * distanceScore)
            + (_searchOptions.AvailabilityWeight * availabilityScore)
            + (_searchOptions.OccupancyWeight * occupancyScore);
        return new RankFactorsResponse(score, distanceScore, availabilityScore, occupancyScore);
    }

    private static LotType MapVehicleType(VehicleType vehicleType)
    {
        return vehicleType switch
        {
            VehicleType.Car => LotType.Car,
            VehicleType.HeavyVehicle => LotType.HeavyVehicle,
            VehicleType.Motorcycle => LotType.Motorcycle,
            _ => throw new ArgumentOutOfRangeException(nameof(vehicleType))
        };
    }

    private static int FreshnessSortValue(string freshness)
    {
        return freshness switch
        {
            "fresh" => 2,
            "stale" => 1,
            _ => 0
        };
    }

    private static string ToCamelCase<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
