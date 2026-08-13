using System.Collections.Immutable;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg;

namespace CarparkAvailability.ApiApp.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class QueueAvailabilityClient(params object[] outcomes) : IAvailabilityClient
{
    private readonly Queue<object> _outcomes = new(outcomes);

    public int FetchCount { get; private set; }

    public Task<AvailabilitySnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        FetchCount++;
        object outcome = _outcomes.Dequeue();
        return outcome switch
        {
            AvailabilitySnapshot snapshot => Task.FromResult(snapshot),
            Exception exception => Task.FromException<AvailabilitySnapshot>(exception),
            _ => throw new InvalidOperationException("Unsupported test outcome.")
        };
    }

    public static AvailabilitySnapshot Snapshot(
        DateTimeOffset sourceTime,
        DateTimeOffset retrievalTime,
        params CarParkAvailability[] records)
    {
        return new AvailabilitySnapshot(
            sourceTime,
            retrievalTime,
            records.ToImmutableDictionary(
                static record => record.CarParkNumber,
                StringComparer.OrdinalIgnoreCase),
            0);
    }
}

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(handler(request, cancellationToken));
    }
}
