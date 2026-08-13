using CarparkAvailability.ApiApp.Contracts;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Options;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Services;

public sealed class DataStatusService(
    CarParkCatalog catalog,
    AvailabilitySnapshotStore store,
    IOptions<AvailabilityOptions> options,
    TimeProvider timeProvider)
{
    private readonly TimeSpan _freshnessThreshold = options.Value.FreshnessThreshold;

    public DataStatusResponse GetStatus()
    {
        AvailabilityStatus status = store.GetStatus();
        AvailabilitySnapshot? snapshot = status.Snapshot;
        int matchedCount = snapshot is null
            ? 0
            : catalog.Records.Count(carPark => snapshot.Records.ContainsKey(carPark.CarParkNumber));
        FreshnessState freshness = FreshnessCalculator.Calculate(
            snapshot?.SourceUpdateTime,
            timeProvider.GetUtcNow(),
            _freshnessThreshold);

        return new DataStatusResponse(
            status.LastAttemptTime,
            status.LastSuccessTime,
            snapshot?.SourceUpdateTime,
            snapshot?.RetrievalTime,
            ToCamelCase(freshness),
            catalog.Records.Length,
            catalog.QuarantinedRowCount,
            snapshot?.Records.Count ?? 0,
            matchedCount,
            snapshot?.ValidationErrorCount ?? 0,
            status.LastError);
    }

    private static string ToCamelCase(FreshnessState freshness)
    {
        string text = freshness.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
