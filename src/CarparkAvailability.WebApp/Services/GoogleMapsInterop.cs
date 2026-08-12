using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace CarparkAvailability.WebApp.Services;

public sealed class GoogleMapsInterop(
    IJSRuntime jsRuntime,
    IOptions<GoogleMapsOptions> options) : IMapInterop
{
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private readonly GoogleMapsOptions _options = options.Value;
    private IJSObjectReference? _module;
    private DotNetObjectReference<object>? _callbackReference;

    public async Task<bool> InitializeAsync(
        ElementReference mapElement,
        object callbackReceiver,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference module = await GetModuleAsync(cancellationToken);
        _callbackReference?.Dispose();
        _callbackReference = DotNetObjectReference.Create(callbackReceiver);
        return await module.InvokeAsync<bool>(
            "initialize",
            cancellationToken,
            mapElement,
            _options.ApiKey,
            _callbackReference);
    }

    public async Task<GeocodeResult> GeocodeAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<GeocodeResult>("geocode", cancellationToken, searchText);
    }

    public async Task<LocationResult> RequestCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        IJSObjectReference module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<LocationResult>("requestCurrentLocation", cancellationToken);
    }

    public async Task<MapLocationLabel?> ResolveLocationAsync(
        MapCoordinate coordinate,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<MapLocationLabel?>("resolveLocation", cancellationToken, coordinate);
    }

    public async Task SetMarkersAsync(
        IReadOnlyCollection<MapMarker> markers,
        MapCoordinate? origin,
        string? originLabel,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("setMarkers", cancellationToken, markers, origin, originLabel);
    }

    public async Task SelectMarkerAsync(
        string? carParkNumber,
        CancellationToken cancellationToken = default)
    {
        IJSObjectReference module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("selectMarker", cancellationToken, carParkNumber);
    }

    public async ValueTask DisposeAsync()
    {
        _callbackReference?.Dispose();
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            "./js/googleMaps.js");
        return _module;
    }
}
