using System.Text.Json;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated;
using CarparkAvailability.ApiApp.Options;
using Microsoft.Extensions.Options;

namespace CarparkAvailability.ApiApp.Services;

public sealed class AvailabilityRefreshService(
    IAvailabilityClient client,
    AvailabilitySnapshotStore store,
    IOptions<AvailabilityOptions> options,
    TimeProvider timeProvider,
    ILogger<AvailabilityRefreshService> logger) : BackgroundService
{
    private readonly TimeSpan _pollInterval = options.Value.PollInterval;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshOnceAsync(stoppingToken);
        using PeriodicTimer timer = new(_pollInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshIfDueAsync(stoppingToken);
        }
    }

    public async Task<bool> RefreshOnceAsync(CancellationToken cancellationToken)
    {
        return await RefreshAsync(ignoreCooldown: true, cancellationToken);
    }

    public async Task<bool> RefreshIfDueAsync(CancellationToken cancellationToken)
    {
        return await RefreshAsync(ignoreCooldown: false, cancellationToken);
    }

    private async Task<bool> RefreshAsync(bool ignoreCooldown, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset attemptTime = timeProvider.GetUtcNow();
            AvailabilityStatus currentStatus = store.GetStatus();
            if (!ignoreCooldown &&
                currentStatus.LastAttemptTime is DateTimeOffset lastAttemptTime &&
                attemptTime - lastAttemptTime < _pollInterval)
            {
                return currentStatus.Snapshot is not null && currentStatus.LastError is null;
            }

            store.RecordAttempt(attemptTime);
            try
            {
                Domain.Availability.AvailabilitySnapshot snapshot = await client.FetchAsync(cancellationToken);
                store.RecordSuccess(snapshot, timeProvider.GetUtcNow());
                logger.LogInformation(
                    "Availability refresh succeeded with {LiveRecordCount} records and {ValidationErrorCount} validation errors.",
                    snapshot.Records.Count,
                    snapshot.ValidationErrorCount);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return RecordFailure("The availability request timed out.");
            }
            catch (HttpRequestException exception)
            {
                return RecordFailure(exception.Message);
            }
            catch (ApiException exception)
            {
                return RecordFailure(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return RecordFailure(exception.Message);
            }
            catch (JsonException exception)
            {
                return RecordFailure(exception.Message);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool RecordFailure(string error)
    {
        store.RecordFailure(error);
        logger.LogWarning("Availability refresh failed; retaining the last-known-good snapshot. {Error}", error);
        return false;
    }
}
