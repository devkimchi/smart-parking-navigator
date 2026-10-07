using System.Globalization;

namespace CarparkAvailability.WebApp.Services;

public static class SingaporeTime
{
    private static readonly TimeSpan s_offset = TimeSpan.FromHours(8);

    public static string Format(DateTimeOffset value)
    {
        string timestamp = value.ToOffset(s_offset).ToString("d MMM, h:mm tt", CultureInfo.InvariantCulture);
        return $"{timestamp} SGT";
    }
}
