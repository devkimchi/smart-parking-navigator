using System.Globalization;
using CarparkAvailability.ApiApp.Contracts;
using CarparkAvailability.ApiApp.Domain.CarParks;
using CarparkAvailability.ApiApp.Domain.Recommendations;
using CarparkAvailability.ApiApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace CarparkAvailability.ApiApp.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapCarParkApi(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder api = endpoints.MapGroup("/api");

        api.MapGet("/carparks", SearchCarParks)
            .WithName("SearchCarParks")
            .Produces<CarParkSearchResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        api.MapGet("/carparks/{carParkNumber}", GetCarPark)
            .WithName("GetCarPark")
            .Produces<CarParkDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        api.MapGet("/data-status", (DataStatusService service) => TypedResults.Ok(service.GetStatus()))
            .WithName("GetDataStatus")
            .Produces<DataStatusResponse>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        api.MapPost("/data-status/refresh", RefreshDataStatus)
            .WithName("RefreshDataStatus")
            .Produces<DataStatusResponse>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> RefreshDataStatus(
        HttpContext context,
        AvailabilityRefreshService refreshService,
        DataStatusService dataStatusService,
        IHostApplicationLifetime applicationLifetime)
    {
        bool succeeded = await refreshService.RefreshIfDueAsync(applicationLifetime.ApplicationStopping);
        return succeeded
            ? TypedResults.Ok(dataStatusService.GetStatus())
            : TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Availability refresh failed",
                detail: "The upstream availability source could not be refreshed. Last-known data remains available.",
                type: "https://httpstatuses.com/503",
                extensions: TraceExtension(context));
    }

    private static IResult SearchCarParks(HttpContext context, CarParkSearchService service)
    {
        Dictionary<string, string[]> errors = [];
        IQueryCollection query = context.Request.Query;

        double? latitude = ParseRequiredDouble(query, "latitude", -90, 90, errors);
        double? longitude = ParseRequiredDouble(query, "longitude", -180, 180, errors);
        VehicleType? vehicleType = ParseVehicleType(query, "vehicleType", true, errors);
        bool availableOnly = ParseOptionalBoolean(query, "availableOnly", false, errors) ?? false;
        bool? nightParking = ParseOptionalBoolean(query, "nightParking", null, errors);
        IReadOnlySet<CarParkType> carParkTypes = ParseCarParkTypes(query, errors);

        if (errors.Count > 0)
        {
            return ValidationProblem(context, errors);
        }

        SearchCriteria criteria = new(
            new GeoCoordinate(latitude!.Value, longitude!.Value),
            vehicleType!.Value,
            availableOnly,
            nightParking,
            carParkTypes);
        return TypedResults.Ok(service.Search(criteria));
    }

    private static IResult GetCarPark(
        HttpContext context,
        string carParkNumber,
        CarParkSearchService service)
    {
        Dictionary<string, string[]> errors = [];
        if (string.IsNullOrWhiteSpace(carParkNumber) || carParkNumber.Trim().Length > 16)
        {
            errors["carParkNumber"] = ["A car park number between 1 and 16 characters is required."];
        }

        VehicleType? vehicleType = ParseVehicleType(
            context.Request.Query,
            "vehicleType",
            false,
            errors) ?? VehicleType.Car;
        double? latitude = ParseOptionalDouble(
            context.Request.Query,
            "latitude",
            -90,
            90,
            errors);
        double? longitude = ParseOptionalDouble(
            context.Request.Query,
            "longitude",
            -180,
            180,
            errors);
        bool? nightParking = ParseOptionalBoolean(context.Request.Query, "nightParking", null, errors);
        IReadOnlySet<CarParkType> carParkTypes = ParseCarParkTypes(context.Request.Query, errors);
        if (latitude.HasValue != longitude.HasValue)
        {
            errors["destination"] = ["Latitude and longitude must be supplied together."];
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(context, errors);
        }

        SearchCriteria? alternativeCriteria = latitude.HasValue && longitude.HasValue
            ? new SearchCriteria(
                new GeoCoordinate(latitude.Value, longitude.Value),
                vehicleType.Value,
                true,
                nightParking,
                carParkTypes)
            : null;
        CarParkDetailResponse? detail = service.GetDetail(
            carParkNumber,
            vehicleType.Value,
            alternativeCriteria);
        return detail is null
            ? TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Car park not found",
                detail: "The requested car park does not exist.",
                type: "https://httpstatuses.com/404",
                extensions: TraceExtension(context))
            : TypedResults.Ok(detail);
    }

    private static double? ParseRequiredDouble(
        IQueryCollection query,
        string name,
        double minimum,
        double maximum,
        Dictionary<string, string[]> errors)
    {
        if (!query.TryGetValue(name, out StringValues values) ||
            values.Count != 1 ||
            !double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value) ||
            value < minimum ||
            value > maximum)
        {
            errors[name] = [$"A single value between {minimum} and {maximum} is required."];
            return null;
        }

        return value;
    }

    private static double? ParseOptionalDouble(
        IQueryCollection query,
        string name,
        double minimum,
        double maximum,
        Dictionary<string, string[]> errors)
    {
        if (!query.TryGetValue(name, out StringValues values) || StringValues.IsNullOrEmpty(values))
        {
            return null;
        }

        if (values.Count != 1 ||
            !double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value) ||
            value < minimum ||
            value > maximum)
        {
            errors[name] = [$"A single value between {minimum} and {maximum} is required."];
            return null;
        }

        return value;
    }

    private static VehicleType? ParseVehicleType(
        IQueryCollection query,
        string name,
        bool required,
        Dictionary<string, string[]> errors)
    {
        if (!query.TryGetValue(name, out StringValues values) || StringValues.IsNullOrEmpty(values))
        {
            if (required)
            {
                errors[name] = ["A vehicle type is required."];
            }

            return null;
        }

        if (values.Count != 1)
        {
            errors[name] = ["A single vehicle type is required."];
            return null;
        }

        VehicleType? parsed = values[0] switch
        {
            "car" => VehicleType.Car,
            "heavyVehicle" => VehicleType.HeavyVehicle,
            "motorcycle" => VehicleType.Motorcycle,
            _ => null
        };
        if (parsed is null)
        {
            errors[name] = ["Vehicle type must be car, heavyVehicle, or motorcycle."];
        }

        return parsed;
    }

    private static bool? ParseOptionalBoolean(
        IQueryCollection query,
        string name,
        bool? defaultValue,
        Dictionary<string, string[]> errors)
    {
        if (!query.TryGetValue(name, out StringValues values) || StringValues.IsNullOrEmpty(values))
        {
            return defaultValue;
        }

        if (values.Count != 1 || !bool.TryParse(values[0], out bool parsed))
        {
            errors[name] = ["A single true or false value is required."];
            return defaultValue;
        }

        return parsed;
    }

    private static IReadOnlySet<CarParkType> ParseCarParkTypes(
        IQueryCollection query,
        Dictionary<string, string[]> errors)
    {
        HashSet<CarParkType> result = [];
        if (!query.TryGetValue("carParkType", out StringValues values))
        {
            return result;
        }

        foreach (string? value in values)
        {
            CarParkType? parsed = value switch
            {
                "surface" => CarParkType.Surface,
                "underground" => CarParkType.Underground,
                "multiStorey" => CarParkType.MultiStorey,
                _ => null
            };
            if (parsed is null)
            {
                errors["carParkType"] = ["Car park type must be surface, underground, or multiStorey."];
                return result;
            }

            result.Add(parsed.Value);
        }

        return result;
    }

    private static IResult ValidationProblem(
        HttpContext context,
        Dictionary<string, string[]> errors)
    {
        return TypedResults.ValidationProblem(
            errors,
            title: "One or more validation errors occurred.",
            type: "https://httpstatuses.com/400",
            extensions: TraceExtension(context));
    }

    private static Dictionary<string, object?> TraceExtension(HttpContext context)
    {
        return new Dictionary<string, object?>
        {
            ["traceId"] = context.TraceIdentifier
        };
    }
}
