namespace CarparkAvailability.WebApp.Services;

public static class SingaporeTime
{
    private static readonly TimeSpan s_offset = TimeSpan.FromHours(8);

    public static string Format(DateTimeOffset value)
    {
        return $"{value.ToOffset(s_offset):d MMM, h:mm tt} SGT";
    }
}
