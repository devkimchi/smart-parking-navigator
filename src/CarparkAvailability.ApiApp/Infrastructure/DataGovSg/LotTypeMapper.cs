using CarparkAvailability.ApiApp.Domain.Availability;

namespace CarparkAvailability.ApiApp.Infrastructure.DataGovSg;

public static class LotTypeMapper
{
    public static LotType Map(string code)
    {
        return code.Trim().ToUpperInvariant() switch
        {
            "C" => LotType.Car,
            "H" => LotType.HeavyVehicle,
            "Y" => LotType.Motorcycle,
            _ => LotType.Other
        };
    }
}
