using CarparkAvailability.WebApp.Generated;
using CarparkAvailability.WebApp.Services;

namespace CarparkAvailability.WebApp.State;

public sealed class ParkingSearchState(ICarparkAvailabilityApiClient apiClient)
{
    private readonly ICarparkAvailabilityApiClient _apiClient = apiClient;
    private readonly List<CarParkSearchResult> _results = [];
    private readonly HashSet<CarParkType> _carParkTypes = [];
    private long _searchGeneration;
    private long _detailGeneration;

    public event Action? Changed;

    public string SearchText { get; set; } = string.Empty;

    public string? DestinationLabel { get; private set; }

    public MapCoordinate? Origin { get; private set; }

    public VehicleType VehicleType { get; private set; } = VehicleType.Car;

    public bool AvailableOnly { get; private set; }

    public bool NightParking { get; private set; }

    public IReadOnlySet<CarParkType> CarParkTypes => _carParkTypes;

    public IReadOnlyList<CarParkSearchResult> Results => _results;

    public string? SelectedCarParkNumber { get; private set; }

    public CarParkDetail? SelectedDetail { get; private set; }

    public string? DetailErrorMessage { get; private set; }

    public bool IsLoading { get; private set; }

    public bool IsLoadingDetail { get; private set; }

    public SearchOutcome Outcome { get; private set; } = SearchOutcome.Idle;

    public string? ErrorMessage { get; private set; }

    public LocationPermissionState LocationState { get; private set; } = LocationPermissionState.NotRequested;

    public string? LocationMessage { get; private set; }

    public MapInitializationStatus MapState { get; private set; } = MapInitializationStatus.NotStarted;

    public string? MapMessage { get; private set; }

    public MapViewport? PendingViewport { get; private set; }

    public DataStatus? DataStatus { get; private set; }

    public bool DataStatusUnavailable { get; private set; }

    public bool IsRefreshingData { get; private set; }

    public string? DataRefreshMessage { get; private set; }

    public bool HasActiveFilters => AvailableOnly || NightParking || _carParkTypes.Count > 0;

    public void SetVehicleType(VehicleType vehicleType)
    {
        InvalidatePendingRequests();
        VehicleType = vehicleType;
        NotifyChanged();
    }

    public void SetAvailableOnly(bool value)
    {
        InvalidatePendingRequests();
        AvailableOnly = value;
        NotifyChanged();
    }

    public void SetNightParking(bool value)
    {
        InvalidatePendingRequests();
        NightParking = value;
        NotifyChanged();
    }

    public void SetCarParkType(CarParkType carParkType, bool enabled)
    {
        InvalidatePendingRequests();
        if (enabled)
        {
            _carParkTypes.Add(carParkType);
        }
        else
        {
            _carParkTypes.Remove(carParkType);
        }

        NotifyChanged();
    }

    public void ClearFilters()
    {
        InvalidatePendingRequests();
        AvailableOnly = false;
        NightParking = false;
        _carParkTypes.Clear();
        NotifyChanged();
    }

    public void SetMapReady(bool ready, string? message = null)
    {
        MapState = ready ? MapInitializationStatus.Ready : MapInitializationStatus.Failed;
        MapMessage = message;
        NotifyChanged();
    }

    public void SetDestinationFailure(SearchOutcome outcome, string message)
    {
        InvalidatePendingRequests();
        Outcome = outcome;
        ErrorMessage = message;
        NotifyChanged();
    }

    public bool BeginLocationRequest()
    {
        if (LocationState is LocationPermissionState.Denied or LocationPermissionState.Requesting)
        {
            return false;
        }

        LocationState = LocationPermissionState.Requesting;
        LocationMessage = "Requesting browser location permission…";
        InvalidatePendingRequests();
        NotifyChanged();
        return true;
    }

    public void SetLocationResult(LocationResult result)
    {
        LocationState = result.Status switch
        {
            LocationResultStatus.Granted => LocationPermissionState.Granted,
            LocationResultStatus.Denied => LocationPermissionState.Denied,
            _ => LocationPermissionState.Error,
        };
        LocationMessage = result.Message;
        NotifyChanged();
    }

    public void OfferViewportSearch(MapViewport viewport)
    {
        if (Origin is null || IsLoading)
        {
            return;
        }

        PendingViewport = viewport;
        NotifyChanged();
    }

    public async Task SearchAsync(
        MapCoordinate origin,
        string destinationLabel,
        CancellationToken cancellationToken = default)
    {
        long generation = ++_searchGeneration;
        _detailGeneration++;
        SelectedCarParkNumber = null;
        SelectedDetail = null;
        DetailErrorMessage = null;
        IsLoadingDetail = false;
        IsLoading = true;
        ErrorMessage = null;
        Outcome = SearchOutcome.Loading;
        PendingViewport = null;
        NotifyChanged();

        try
        {
            VehicleType vehicleType = VehicleType;
            bool availableOnly = AvailableOnly;
            bool? nightParking = NightParking ? true : null;
            CarParkType[] carParkTypes = [.. _carParkTypes.OrderBy(static type => type)];
            bool hasActiveFilters = availableOnly || nightParking.HasValue || carParkTypes.Length > 0;
            CarParkSearchResponse response = await _apiClient.SearchCarParksAsync(
                origin.Latitude,
                origin.Longitude,
                vehicleType,
                availableOnly,
                nightParking,
                carParkTypes,
                cancellationToken);
            if (generation != _searchGeneration)
            {
                return;
            }

            Origin = origin;
            DestinationLabel = destinationLabel;
            _results.Clear();
            _results.AddRange(response.Results);
            Outcome = _results.Count == 0
                ? hasActiveFilters ? SearchOutcome.NoFilteredResults : SearchOutcome.NoNearbyCarParks
                : SearchOutcome.Results;
        }
        catch (ApiException exception)
        {
            if (generation != _searchGeneration)
            {
                return;
            }

            Outcome = SearchOutcome.ApiFailure;
            ErrorMessage = ApiErrorMessage(exception.StatusCode);
        }
        catch (HttpRequestException)
        {
            if (generation != _searchGeneration)
            {
                return;
            }

            Outcome = SearchOutcome.ApiFailure;
            ErrorMessage = "Parking information is temporarily unavailable. Existing results may be out of date.";
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                IsLoading = false;
                NotifyChanged();
            }
        }
    }

    public async Task SelectAsync(string carParkNumber, CancellationToken cancellationToken = default)
    {
        long generation = ++_detailGeneration;
        SelectedCarParkNumber = carParkNumber;
        SelectedDetail = null;
        DetailErrorMessage = null;
        IsLoadingDetail = true;
        NotifyChanged();

        try
        {
            MapCoordinate? origin = Origin;
            CarParkDetail detail = await _apiClient.GetCarParkAsync(
                carParkNumber,
                VehicleType,
                origin?.Latitude,
                origin?.Longitude,
                NightParking ? true : null,
                _carParkTypes.OrderBy(static type => type),
                cancellationToken);
            if (generation != _detailGeneration ||
                !string.Equals(SelectedCarParkNumber, carParkNumber, StringComparison.Ordinal))
            {
                return;
            }

            SelectedDetail = detail;
        }
        catch (ApiException exception)
        {
            if (generation != _detailGeneration)
            {
                return;
            }

            DetailErrorMessage = exception.StatusCode == 404
                ? "This car park is no longer available."
                : ApiErrorMessage(exception.StatusCode);
        }
        catch (HttpRequestException)
        {
            if (generation != _detailGeneration)
            {
                return;
            }

            DetailErrorMessage = "Car park details could not be loaded. Please close this message and try again.";
        }
        finally
        {
            if (generation == _detailGeneration)
            {
                IsLoadingDetail = false;
                NotifyChanged();
            }
        }
    }

    public void CloseDetail()
    {
        _detailGeneration++;
        SelectedCarParkNumber = null;
        SelectedDetail = null;
        DetailErrorMessage = null;
        IsLoadingDetail = false;
        NotifyChanged();
    }

    private void InvalidatePendingRequests()
    {
        _searchGeneration++;
        _detailGeneration++;
        IsLoading = false;
        IsLoadingDetail = false;
        SelectedCarParkNumber = null;
        SelectedDetail = null;
        DetailErrorMessage = null;
    }

    public async Task LoadDataStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DataStatus = await _apiClient.GetDataStatusAsync(cancellationToken);
            DataStatusUnavailable = false;
        }
        catch (ApiException)
        {
            DataStatusUnavailable = true;
        }
        catch (HttpRequestException)
        {
            DataStatusUnavailable = true;
        }

        NotifyChanged();
    }

    public async Task<bool> RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        if (IsRefreshingData)
        {
            return false;
        }

        IsRefreshingData = true;
        DataRefreshMessage = null;
        NotifyChanged();

        try
        {
            DataStatus = await _apiClient.RefreshDataStatusAsync(cancellationToken);
            DataStatusUnavailable = false;
            DataRefreshMessage = DataStatus.Freshness == Freshness.Fresh
                ? "Live parking data refreshed."
                : "The upstream source is still stale; the latest known data remains displayed.";
            return true;
        }
        catch (ApiException)
        {
            DataRefreshMessage = "The refresh failed. Last-known parking data remains displayed.";
            return false;
        }
        catch (HttpRequestException)
        {
            DataRefreshMessage = "The refresh service is temporarily unavailable. Last-known parking data remains displayed.";
            return false;
        }
        finally
        {
            IsRefreshingData = false;
            NotifyChanged();
        }
    }

    public async Task<bool> RefreshDataAndCurrentSearchAsync(CancellationToken cancellationToken = default)
    {
        long searchGeneration = _searchGeneration;
        MapCoordinate? origin = Origin;
        string destinationLabel = DestinationLabel ?? "Selected area";

        bool refreshed = await RefreshDataAsync(cancellationToken);
        if (!refreshed || origin is null || searchGeneration != _searchGeneration)
        {
            return false;
        }

        await SearchAsync(origin.Value, destinationLabel, cancellationToken);
        await LoadDataStatusAsync(cancellationToken);
        return true;
    }

    private static string ApiErrorMessage(int statusCode)
    {
        return statusCode switch
        {
            400 => "The search area or filters were not accepted. Adjust them and try again.",
            503 => "Parking data is not ready yet. Please try again shortly.",
            _ => "Parking information could not be loaded. Please try again.",
        };
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}

public enum SearchOutcome
{
    Idle,
    Loading,
    Results,
    NoDestinationMatch,
    AmbiguousDestination,
    NoNearbyCarParks,
    NoFilteredResults,
    ApiFailure,
    MapFailure,
}

public enum LocationPermissionState
{
    NotRequested,
    Requesting,
    Granted,
    Denied,
    Error,
}
