using CarparkAvailability.ApiApp.Domain.CarParks;

namespace CarparkAvailability.ApiApp.Infrastructure.Geospatial;

public static class GeospatialDistance
{
    public const double EarthRadiusMetres = 6371008.8;

    public static double HaversineMetres(GeoCoordinate first, GeoCoordinate second)
    {
        double latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        double longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        double firstLatitude = ToRadians(first.Latitude);
        double secondLatitude = ToRadians(second.Latitude);
        double a = (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2))
            + (Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
                * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));
        double centralAngle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMetres * centralAngle;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
