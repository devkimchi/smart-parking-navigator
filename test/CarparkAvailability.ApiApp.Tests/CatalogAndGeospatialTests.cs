using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Infrastructure.Csv;
using CarparkAvailability.ApiApp.Infrastructure.Geospatial;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarparkAvailability.ApiApp.Tests;

public sealed class CatalogAndGeospatialTests
{
    private const string Header =
        "car_park_no,address,x_coord,y_coord,car_park_type,type_of_parking_system,short_term_parking,free_parking,night_parking,car_park_decks,gantry_height,car_park_basement";

    [Fact]
    public void LoaderMapsExplicitHeadersAndRetainsDetailFields()
    {
        string path = WriteCsv(
            Header,
            "ABC,Sample address,28001.642,38744.572,BASEMENT CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,YES,2,1.9,Y");

        try
        {
            CarParkCatalog catalog = CreateLoader().Load(path);

            CarPark carPark = Assert.Single(catalog.Records);
            Assert.Equal("ABC", carPark.CarParkNumber);
            Assert.Equal(CarParkType.Underground, carPark.Type);
            Assert.Equal("WHOLE DAY", carPark.ShortTermParking);
            Assert.Equal("NO", carPark.FreeParking);
            Assert.True(carPark.NightParking);
            Assert.True(carPark.Basement);
            Assert.Equal(2, carPark.CarParkDecks);
            Assert.Equal(1.9, carPark.GantryHeight);
            Assert.Equal(0, catalog.QuarantinedRowCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BundledCatalogIsUsable()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "HDBCarparkInformation.csv");

        CarParkCatalog catalog = CreateLoader().Load(path);

        Assert.True(catalog.Records.Length > 2000);
        Assert.Equal(0, catalog.QuarantinedRowCount);
    }

    [Fact]
    public void LoaderQuarantinesDuplicateAndMalformedRows()
    {
        string path = WriteCsv(
            Header,
            "ABC,Sample address,28001.642,38744.572,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,NO,1,2.0,N",
            "abc,Duplicate,28001.642,38744.572,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,NO,1,2.0,N",
            "BAD,Bad coordinate,not-a-number,38744.572,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,NO,1,2.0,N");

        try
        {
            CarParkCatalog catalog = CreateLoader().Load(path);

            Assert.Single(catalog.Records);
            Assert.Equal(2, catalog.QuarantinedRowCount);
            Assert.True(catalog.TryGet(" abc ", out CarPark? mapped));
            Assert.NotNull(mapped);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoaderRejectsMissingHeaderAndUnusableCatalog()
    {
        string missingHeaderPath = WriteCsv(
            Header.Replace(",address", string.Empty, StringComparison.Ordinal),
            "ABC,28001.642,38744.572,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,NO,1,2.0,N");
        string unusablePath = WriteCsv(
            Header,
            "BAD,Address,x,y,SURFACE CAR PARK,ELECTRONIC PARKING,WHOLE DAY,NO,NO,1,2.0,N");

        try
        {
            Assert.Throws<InvalidDataException>(() => CreateLoader().Load(missingHeaderPath));
            Assert.Throws<InvalidDataException>(() => CreateLoader().Load(unusablePath));
        }
        finally
        {
            File.Delete(missingHeaderPath);
            File.Delete(unusablePath);
        }
    }

    [Fact]
    public void Svy21OriginConvertsToDocumentedWgs84Origin()
    {
        Svy21CoordinateConverter converter = new();

        GeoCoordinate coordinate = converter.Convert(28001.642, 38744.572);

        Assert.Equal(1.366666, coordinate.Latitude, 6);
        Assert.Equal(103.833333, coordinate.Longitude, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => converter.Convert(0, 0));
    }

    [Fact]
    public void HaversinePreservesFiveHundredMetreBoundary()
    {
        GeoCoordinate origin = new(1.366666, 103.833333);
        double latitudeDeltaAtFiveHundredMetres =
            500 / GeospatialDistance.EarthRadiusMetres * 180 / Math.PI;
        GeoCoordinate boundary = new(
            origin.Latitude + latitudeDeltaAtFiveHundredMetres,
            origin.Longitude);
        GeoCoordinate outside = new(
            origin.Latitude + (latitudeDeltaAtFiveHundredMetres * 1.001),
            origin.Longitude);

        Assert.Equal(500, GeospatialDistance.HaversineMetres(origin, boundary), 6);
        Assert.True(GeospatialDistance.HaversineMetres(origin, boundary) <= 500.000001);
        Assert.True(GeospatialDistance.HaversineMetres(origin, outside) > 500);
    }

    private static CarParkCatalogLoader CreateLoader()
    {
        return new CarParkCatalogLoader(
            new Svy21CoordinateConverter(),
            NullLogger<CarParkCatalogLoader>.Instance);
    }

    internal static string WriteCsv(params string[] lines)
    {
        string path = Path.Combine(AppContext.BaseDirectory, $"catalog-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(path, lines);
        return path;
    }
}
