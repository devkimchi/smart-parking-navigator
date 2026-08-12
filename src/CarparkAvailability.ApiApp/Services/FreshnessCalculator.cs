using CarparkAvailability.ApiApp.Domain.Availability;

namespace CarparkAvailability.ApiApp.Services;

public static class FreshnessCalculator
{
    public static FreshnessState Calculate(
        DateTimeOffset? sourceUpdateTime,
        DateTimeOffset now,
        TimeSpan freshnessThreshold)
    {
        if (sourceUpdateTime is null)
        {
            return FreshnessState.Unavailable;
        }

        return now - sourceUpdateTime.Value <= freshnessThreshold
            ? FreshnessState.Fresh
            : FreshnessState.Stale;
    }
}
