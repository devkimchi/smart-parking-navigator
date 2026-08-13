namespace CarparkAvailability.ApiApp.Domain.CarParks;

public enum CarParkType
{
    Surface,
    Underground,
    MultiStorey
}

public sealed record GeoCoordinate(double Latitude, double Longitude);

public sealed record CarPark(
    string CarParkNumber,
    string Address,
    GeoCoordinate Coordinate,
    CarParkType Type,
    string SourceCarParkType,
    string ParkingSystem,
    string ShortTermParking,
    string FreeParking,
    bool NightParking,
    int CarParkDecks,
    double GantryHeight,
    bool Basement);
