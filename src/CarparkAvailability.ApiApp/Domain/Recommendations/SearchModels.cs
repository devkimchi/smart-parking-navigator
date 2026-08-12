using CarparkAvailability.ApiApp.Domain.CarParks;

namespace CarparkAvailability.ApiApp.Domain.Recommendations;

public enum VehicleType
{
    Car,
    HeavyVehicle,
    Motorcycle
}

public sealed record SearchCriteria(
    GeoCoordinate Origin,
    VehicleType VehicleType,
    bool AvailableOnly,
    bool? NightParking,
    IReadOnlySet<CarParkType> CarParkTypes);
