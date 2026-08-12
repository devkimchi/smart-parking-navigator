using CarparkAvailability.ApiApp.Domain.CarParks;

namespace CarparkAvailability.ApiApp.Infrastructure.Geospatial;

public sealed class Svy21CoordinateConverter
{
    private const double SemiMajorAxis = 6378137.0;
    private const double Flattening = 1.0 / 298.257223563;
    private const double OriginLatitudeDegrees = 1.366666;
    private const double OriginLongitudeDegrees = 103.833333;
    private const double FalseNorthing = 38744.572;
    private const double FalseEasting = 28001.642;
    private const double ScaleFactor = 1.0;
    private const double MinimumLatitude = 1.13;
    private const double MaximumLatitude = 1.48;
    private const double MinimumLongitude = 103.59;
    private const double MaximumLongitude = 104.10;

    private static readonly double s_eccentricitySquared = (2 * Flattening) - (Flattening * Flattening);
    private static readonly double s_originMeridionalArc = CalculateMeridionalArc(ToRadians(OriginLatitudeDegrees));

    public GeoCoordinate Convert(double easting, double northing)
    {
        if (!double.IsFinite(easting) || !double.IsFinite(northing))
        {
            throw new ArgumentOutOfRangeException(nameof(easting), "SVY21 coordinates must be finite.");
        }

        double meridionalArc = s_originMeridionalArc + ((northing - FalseNorthing) / ScaleFactor);
        double n = (1 - Math.Sqrt(1 - s_eccentricitySquared)) / (1 + Math.Sqrt(1 - s_eccentricitySquared));
        double mu = meridionalArc /
            (SemiMajorAxis * (1 - (s_eccentricitySquared / 4) -
                (3 * Math.Pow(s_eccentricitySquared, 2) / 64) -
                (5 * Math.Pow(s_eccentricitySquared, 3) / 256)));

        double footprintLatitude = mu
            + (((3 * n / 2) - (27 * Math.Pow(n, 3) / 32)) * Math.Sin(2 * mu))
            + (((21 * Math.Pow(n, 2) / 16) - (55 * Math.Pow(n, 4) / 32)) * Math.Sin(4 * mu))
            + ((151 * Math.Pow(n, 3) / 96) * Math.Sin(6 * mu))
            + ((1097 * Math.Pow(n, 4) / 512) * Math.Sin(8 * mu));

        double sinFootprint = Math.Sin(footprintLatitude);
        double cosFootprint = Math.Cos(footprintLatitude);
        double tanFootprint = Math.Tan(footprintLatitude);
        double eccentricityPrimeSquared = s_eccentricitySquared / (1 - s_eccentricitySquared);
        double c1 = eccentricityPrimeSquared * cosFootprint * cosFootprint;
        double t1 = tanFootprint * tanFootprint;
        double radiusPrimeVertical = SemiMajorAxis / Math.Sqrt(1 - (s_eccentricitySquared * sinFootprint * sinFootprint));
        double radiusMeridian = SemiMajorAxis * (1 - s_eccentricitySquared) /
            Math.Pow(1 - (s_eccentricitySquared * sinFootprint * sinFootprint), 1.5);
        double d = (easting - FalseEasting) / (radiusPrimeVertical * ScaleFactor);

        double latitude = footprintLatitude -
            ((radiusPrimeVertical * tanFootprint) / radiusMeridian) *
            ((d * d / 2)
            - ((5 + (3 * t1) + (10 * c1) - (4 * c1 * c1) - (9 * eccentricityPrimeSquared)) * Math.Pow(d, 4) / 24)
            + ((61 + (90 * t1) + (298 * c1) + (45 * t1 * t1) - (252 * eccentricityPrimeSquared) - (3 * c1 * c1))
                * Math.Pow(d, 6) / 720));

        double longitude = ToRadians(OriginLongitudeDegrees) +
            (d
            - ((1 + (2 * t1) + c1) * Math.Pow(d, 3) / 6)
            + ((5 - (2 * c1) + (28 * t1) - (3 * c1 * c1) + (8 * eccentricityPrimeSquared) + (24 * t1 * t1))
                * Math.Pow(d, 5) / 120)) / cosFootprint;

        GeoCoordinate coordinate = new(ToDegrees(latitude), ToDegrees(longitude));
        if (coordinate.Latitude < MinimumLatitude || coordinate.Latitude > MaximumLatitude ||
            coordinate.Longitude < MinimumLongitude || coordinate.Longitude > MaximumLongitude)
        {
            throw new ArgumentOutOfRangeException(nameof(easting), "Converted coordinate is outside Singapore bounds.");
        }

        return coordinate;
    }

    private static double CalculateMeridionalArc(double latitude)
    {
        double e2 = s_eccentricitySquared;
        double e4 = e2 * e2;
        double e6 = e4 * e2;

        return SemiMajorAxis *
            (((1 - (e2 / 4) - (3 * e4 / 64) - (5 * e6 / 256)) * latitude)
            - (((3 * e2 / 8) + (3 * e4 / 32) + (45 * e6 / 1024)) * Math.Sin(2 * latitude))
            + (((15 * e4 / 256) + (45 * e6 / 1024)) * Math.Sin(4 * latitude))
            - ((35 * e6 / 3072) * Math.Sin(6 * latitude)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
