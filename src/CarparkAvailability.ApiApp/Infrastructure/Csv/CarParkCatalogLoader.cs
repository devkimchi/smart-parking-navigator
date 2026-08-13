using System.Globalization;
using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Infrastructure.Geospatial;
using CsvHelper;
using CsvHelper.Configuration;

namespace CarparkAvailability.ApiApp.Infrastructure.Csv;

public sealed class CarParkCatalogLoader(
    Svy21CoordinateConverter coordinateConverter,
    ILogger<CarParkCatalogLoader> logger)
{
    private static readonly string[] s_requiredHeaders =
    [
        "car_park_no", "address", "x_coord", "y_coord", "car_park_type",
        "type_of_parking_system", "short_term_parking", "free_parking",
        "night_parking", "car_park_decks", "gantry_height", "car_park_basement"
    ];

    public CarParkCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Required HDB car park catalog was not found.", path);
        }

        CsvConfiguration configuration = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };

        List<CarPark> carParks = [];
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        int quarantinedCount = 0;

        using StreamReader reader = File.OpenText(path);
        using CsvReader csv = new(reader, configuration);
        csv.Context.RegisterClassMap<HdbCarParkCsvMap>();

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new InvalidDataException("The HDB car park catalog has no header.");
        }

        string[] headers = csv.HeaderRecord ?? [];
        string[] missingHeaders = s_requiredHeaders
            .Where(required => !headers.Contains(required, StringComparer.Ordinal))
            .ToArray();
        if (missingHeaders.Length > 0)
        {
            throw new InvalidDataException($"The HDB car park catalog is missing required headers: {string.Join(", ", missingHeaders)}.");
        }

        while (csv.Read())
        {
            HdbCarParkCsvRow? row;
            try
            {
                row = csv.GetRecord<HdbCarParkCsvRow>();
            }
            catch (CsvHelperException exception)
            {
                quarantinedCount++;
                logger.LogWarning(
                    exception,
                    "Quarantined malformed static car park row {RowNumber}.",
                    csv.Context.Parser?.Row ?? -1);
                continue;
            }

            string reason = "empty row";
            if (row is null ||
                !TryMap(row, identifiers, out CarPark? carPark, out reason) ||
                carPark is null)
            {
                quarantinedCount++;
                logger.LogWarning(
                    "Quarantined static car park row {RowNumber}: {Reason}.",
                    csv.Context.Parser?.Row ?? -1,
                    row is null ? "empty row" : reason);
                continue;
            }

            carParks.Add(carPark);
        }

        if (carParks.Count == 0)
        {
            throw new InvalidDataException("The HDB car park catalog contains no usable records.");
        }

        logger.LogInformation(
            "Loaded {StaticRecordCount} static car parks; quarantined {QuarantinedRowCount} rows.",
            carParks.Count,
            quarantinedCount);
        return new CarParkCatalog(carParks, quarantinedCount);
    }

    private bool TryMap(
        HdbCarParkCsvRow row,
        HashSet<string> identifiers,
        out CarPark? carPark,
        out string reason)
    {
        carPark = null;
        string identifier = row.CarParkNumber.Trim();
        if (identifier.Length == 0)
        {
            reason = "missing car park identifier";
            return false;
        }

        if (!identifiers.Add(identifier))
        {
            reason = "duplicate car park identifier";
            return false;
        }

        if (string.IsNullOrWhiteSpace(row.Address) || string.IsNullOrWhiteSpace(row.ParkingSystem))
        {
            reason = "missing required descriptive field";
            return false;
        }

        if (!double.TryParse(row.XCoordinate, NumberStyles.Float, CultureInfo.InvariantCulture, out double easting) ||
            !double.TryParse(row.YCoordinate, NumberStyles.Float, CultureInfo.InvariantCulture, out double northing) ||
            !int.TryParse(row.CarParkDecks, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decks) ||
            decks < 0 ||
            !double.TryParse(row.GantryHeight, NumberStyles.Float, CultureInfo.InvariantCulture, out double gantryHeight) ||
            gantryHeight < 0)
        {
            reason = "invalid numeric field";
            return false;
        }

        if (!TryNormalizeType(row.CarParkType, out CarParkType type) ||
            !TryParseYesNo(row.NightParking, out bool nightParking) ||
            !TryParseYesNo(row.CarParkBasement, out bool basement))
        {
            reason = "invalid normalized value";
            return false;
        }

        GeoCoordinate coordinate;
        try
        {
            coordinate = coordinateConverter.Convert(easting, northing);
        }
        catch (ArgumentOutOfRangeException)
        {
            reason = "invalid coordinate";
            return false;
        }

        carPark = new CarPark(
            identifier,
            row.Address.Trim(),
            coordinate,
            type,
            row.CarParkType.Trim(),
            row.ParkingSystem.Trim(),
            row.ShortTermParking.Trim(),
            row.FreeParking.Trim(),
            nightParking,
            decks,
            gantryHeight,
            basement);
        reason = string.Empty;
        return true;
    }

    private static bool TryNormalizeType(string sourceValue, out CarParkType type)
    {
        string normalized = sourceValue.Trim().ToUpperInvariant();
        if (normalized.Contains("MULTI-STOREY", StringComparison.Ordinal) ||
            normalized.Contains("MECHANISED", StringComparison.Ordinal) &&
            !normalized.Contains("SURFACE", StringComparison.Ordinal))
        {
            type = CarParkType.MultiStorey;
            return true;
        }

        if (normalized.Contains("BASEMENT", StringComparison.Ordinal))
        {
            type = CarParkType.Underground;
            return true;
        }

        if (normalized.Contains("SURFACE", StringComparison.Ordinal) ||
            normalized.Contains("COVERED", StringComparison.Ordinal))
        {
            type = CarParkType.Surface;
            return true;
        }

        type = default;
        return false;
    }

    private static bool TryParseYesNo(string value, out bool result)
    {
        if (string.Equals(value.Trim(), "YES", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value.Trim(), "Y", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(value.Trim(), "NO", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value.Trim(), "N", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }
}
