namespace CarparkAvailability.ApiApp.Infrastructure.Csv;

internal sealed class HdbCarParkCsvRow
{
    public string CarParkNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string XCoordinate { get; set; } = string.Empty;
    public string YCoordinate { get; set; } = string.Empty;
    public string CarParkType { get; set; } = string.Empty;
    public string ParkingSystem { get; set; } = string.Empty;
    public string ShortTermParking { get; set; } = string.Empty;
    public string FreeParking { get; set; } = string.Empty;
    public string NightParking { get; set; } = string.Empty;
    public string CarParkDecks { get; set; } = string.Empty;
    public string GantryHeight { get; set; } = string.Empty;
    public string CarParkBasement { get; set; } = string.Empty;
}
