namespace CarparkAvailability.ApiApp.Contracts;

public sealed record CoordinateResponse(double Latitude, double Longitude);

public sealed record LotAvailabilityResponse(string LotType, int TotalLots, int AvailableLots);

public sealed record AvailabilityResponse(
    IReadOnlyList<LotAvailabilityResponse> Lots,
    DateTimeOffset? SourceUpdateTime,
    DateTimeOffset? RetrievalTime,
    string Freshness,
    double? OccupancyRate);

public sealed record RankFactorsResponse(
    double Score,
    double DistanceScore,
    double AvailabilityScore,
    double OccupancyScore);

public sealed record CarParkSearchResultResponse(
    string CarParkNumber,
    string Address,
    CoordinateResponse Coordinate,
    string CarParkType,
    string SourceCarParkType,
    string ParkingSystem,
    bool NightParking,
    int? Decks,
    double? GantryHeight,
    bool Basement,
    string? ShortTermParking,
    string? FreeParking,
    bool IncompleteMetadata,
    double DistanceMetres,
    AvailabilityResponse Availability,
    bool Compatible,
    bool Recommended,
    IReadOnlyList<string> ExclusionReasons,
    RankFactorsResponse? RankFactors);

public sealed record CarParkSearchResponse(
    IReadOnlyList<CarParkSearchResultResponse> Results,
    double RadiusMetres);

public sealed record CarParkDetailResponse(
    string CarParkNumber,
    string Address,
    CoordinateResponse Coordinate,
    string CarParkType,
    string SourceCarParkType,
    string ParkingSystem,
    bool NightParking,
    int? Decks,
    double? GantryHeight,
    bool Basement,
    string? ShortTermParking,
    string? FreeParking,
    bool IncompleteMetadata,
    AvailabilityResponse Availability,
    bool Compatible,
    IReadOnlyList<string> ExclusionReasons,
    IReadOnlyList<CarParkSearchResultResponse> Alternatives);

public sealed record DataStatusResponse(
    DateTimeOffset? LastAttemptTime,
    DateTimeOffset? LastSuccessTime,
    DateTimeOffset? SourceUpdateTime,
    DateTimeOffset? RetrievalTime,
    string Freshness,
    int StaticRecordCount,
    int QuarantinedStaticRowCount,
    int LiveRecordCount,
    int MatchedRecordCount,
    int ValidationErrorCount,
    string? LastError);
