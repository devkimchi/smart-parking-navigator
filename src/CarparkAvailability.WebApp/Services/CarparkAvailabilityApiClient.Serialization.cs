using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarparkAvailability.WebApp.Generated;

public partial class CarparkAvailabilityApiClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }
}
