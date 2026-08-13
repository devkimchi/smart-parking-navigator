using CsvHelper.Configuration;

namespace CarparkAvailability.ApiApp.Infrastructure.Csv;

internal sealed class HdbCarParkCsvMap : ClassMap<HdbCarParkCsvRow>
{
    public HdbCarParkCsvMap()
    {
        Map(static row => row.CarParkNumber).Name("car_park_no");
        Map(static row => row.Address).Name("address");
        Map(static row => row.XCoordinate).Name("x_coord");
        Map(static row => row.YCoordinate).Name("y_coord");
        Map(static row => row.CarParkType).Name("car_park_type");
        Map(static row => row.ParkingSystem).Name("type_of_parking_system");
        Map(static row => row.ShortTermParking).Name("short_term_parking");
        Map(static row => row.FreeParking).Name("free_parking");
        Map(static row => row.NightParking).Name("night_parking");
        Map(static row => row.CarParkDecks).Name("car_park_decks");
        Map(static row => row.GantryHeight).Name("gantry_height");
        Map(static row => row.CarParkBasement).Name("car_park_basement");
    }
}
