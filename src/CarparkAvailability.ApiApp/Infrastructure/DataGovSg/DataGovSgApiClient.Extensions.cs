using System.Text.Json.Serialization;

namespace CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated;

public partial class Carpark_data
{
    [JsonPropertyName("carpark_number")]
    public string? CarparkNumber { get; set; }

    [JsonPropertyName("update_datetime")]
    public string? UpdateDatetime { get; set; }

    [JsonPropertyName("carpark_info")]
    public ICollection<DataGovSgCarparkLot>? CarparkInfo { get; set; }
}

public sealed class DataGovSgCarparkLot
{
    [JsonPropertyName("total_lots")]
    public string? TotalLots { get; set; }

    [JsonPropertyName("lot_type")]
    public string? LotType { get; set; }

    [JsonPropertyName("lots_available")]
    public string? LotsAvailable { get; set; }
}
