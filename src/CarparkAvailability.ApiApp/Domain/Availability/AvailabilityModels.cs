using System.Collections.Immutable;

namespace CarparkAvailability.ApiApp.Domain.Availability;

public enum LotType
{
    Car,
    HeavyVehicle,
    Motorcycle,
    Other
}

public enum FreshnessState
{
    Fresh,
    Stale,
    Unavailable
}

public sealed record LotAvailability(LotType LotType, int TotalLots, int AvailableLots)
{
    public double? OccupancyRate => TotalLots > 0
        ? (double)(TotalLots - AvailableLots) / TotalLots
        : null;
}

public sealed record CarParkAvailability(
    string CarParkNumber,
    DateTimeOffset UpdateTime,
    ImmutableArray<LotAvailability> Lots);

public sealed record AvailabilitySnapshot(
    DateTimeOffset SourceUpdateTime,
    DateTimeOffset RetrievalTime,
    ImmutableDictionary<string, CarParkAvailability> Records,
    int ValidationErrorCount);

public sealed record AvailabilityStatus(
    AvailabilitySnapshot? Snapshot,
    DateTimeOffset? LastAttemptTime,
    DateTimeOffset? LastSuccessTime,
    string? LastError);
