namespace CarparkAvailability.ApiApp.Options;

public sealed class AvailabilityOptions
{
    public const string SectionName = "Availability";

    public Uri BaseUrl { get; set; } = new("https://api.data.gov.sg/v1/");

    public string? ApiKey { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan FreshnessThreshold { get; set; } = TimeSpan.FromSeconds(120);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
