namespace CarparkAvailability.ApiApp.Options;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public double RadiusMetres { get; set; } = 500;

    public double DistanceWeight { get; set; } = 0.50;

    public double AvailabilityWeight { get; set; } = 0.30;

    public double OccupancyWeight { get; set; } = 0.20;

    public int AvailableLotCap { get; set; } = 20;
}
