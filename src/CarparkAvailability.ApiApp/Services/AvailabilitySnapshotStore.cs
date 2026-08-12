using CarparkAvailability.ApiApp.Domain.Availability;

namespace CarparkAvailability.ApiApp.Services;

public sealed class AvailabilitySnapshotStore
{
    private AvailabilityStatus _status = new(null, null, null, null);

    public AvailabilityStatus GetStatus() => Volatile.Read(ref _status);

    public void RecordAttempt(DateTimeOffset attemptTime)
    {
        AvailabilityStatus current = GetStatus();
        Volatile.Write(
            ref _status,
            current with
            {
                LastAttemptTime = attemptTime,
                LastError = null
            });
    }

    public void RecordSuccess(AvailabilitySnapshot snapshot, DateTimeOffset successTime)
    {
        AvailabilityStatus current = GetStatus();
        Volatile.Write(
            ref _status,
            new AvailabilityStatus(snapshot, current.LastAttemptTime, successTime, null));
    }

    public void RecordFailure(string error)
    {
        AvailabilityStatus current = GetStatus();
        Volatile.Write(ref _status, current with { LastError = error });
    }
}
