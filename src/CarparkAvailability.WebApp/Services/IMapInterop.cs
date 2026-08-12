using Microsoft.AspNetCore.Components;

namespace CarparkAvailability.WebApp.Services;

public interface IMapInterop : IAsyncDisposable
{
    Task<bool> InitializeAsync(ElementReference mapElement, object callbackReceiver, CancellationToken cancellationToken = default);

    Task<GeocodeResult> GeocodeAsync(string searchText, CancellationToken cancellationToken = default);

    Task<LocationResult> RequestCurrentLocationAsync(CancellationToken cancellationToken = default);

    Task<MapLocationLabel?> ResolveLocationAsync(
        MapCoordinate coordinate,
        CancellationToken cancellationToken = default);

    Task SetMarkersAsync(
        IReadOnlyCollection<MapMarker> markers,
        MapCoordinate? origin,
        string? originLabel,
        CancellationToken cancellationToken = default);

    Task SelectMarkerAsync(string? carParkNumber, CancellationToken cancellationToken = default);
}
