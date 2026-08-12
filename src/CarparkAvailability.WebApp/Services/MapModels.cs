namespace CarparkAvailability.WebApp.Services;

using System.Text.Json.Serialization;

public readonly record struct MapCoordinate(double Latitude, double Longitude)
{
    public bool IsWithinSingapore =>
        Latitude is >= 1.13 and <= 1.48 &&
        Longitude is >= 103.59 and <= 104.10;
}

public sealed record MapMarker(
    string CarParkNumber,
    string Label,
    double Latitude,
    double Longitude,
    int? AvailableLots,
    bool Recommended,
    bool Selected);

public sealed record GeocodeResult(
    GeocodeResultStatus Status,
    MapCoordinate? Coordinate,
    string? ResolvedLabel,
    string? Message,
    IReadOnlyList<GeocodeCandidate>? Candidates = null);

public sealed record GeocodeCandidate(
    string Name,
    string? Address,
    MapCoordinate Coordinate);

[JsonConverter(typeof(JsonStringEnumConverter<GeocodeResultStatus>))]
public enum GeocodeResultStatus
{
    Resolved,
    NoMatch,
    Ambiguous,
    Failed,
}

public sealed record LocationResult(
    LocationResultStatus Status,
    MapCoordinate? Coordinate,
    string? Message);

public sealed record MapLocationLabel(
    string Name,
    string? Address);

[JsonConverter(typeof(JsonStringEnumConverter<LocationResultStatus>))]
public enum LocationResultStatus
{
    Granted,
    Denied,
    Unavailable,
    Failed,
}

public sealed record MapViewport(double CenterLatitude, double CenterLongitude);

public enum MapInitializationStatus
{
    NotStarted,
    Ready,
    Failed,
}
