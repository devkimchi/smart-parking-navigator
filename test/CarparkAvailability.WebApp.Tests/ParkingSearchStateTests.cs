using CarparkAvailability.WebApp.Generated;
using CarparkAvailability.WebApp.Services;
using CarparkAvailability.WebApp.State;

namespace CarparkAvailability.WebApp.Tests;

public sealed class ParkingSearchStateTests
{
    [Fact]
    public async Task SearchMapsVehicleAndFiltersToGeneratedClient()
    {
        VehicleType capturedVehicle = VehicleType.Car;
        bool? capturedAvailableOnly = null;
        bool? capturedNightParking = null;
        IReadOnlyCollection<CarParkType> capturedTypes = [];
        FakeApiClient apiClient = new()
        {
            Search = (_, _, vehicle, availableOnly, nightParking, carParkTypes) =>
            {
                capturedVehicle = vehicle;
                capturedAvailableOnly = availableOnly;
                capturedNightParking = nightParking;
                capturedTypes = carParkTypes?.ToArray() ?? [];
                return new CarParkSearchResponse { Results = [TestData.SearchResult()] };
            },
        };
        ParkingSearchState state = new(apiClient);
        state.SetVehicleType(VehicleType.HeavyVehicle);
        state.SetAvailableOnly(true);
        state.SetNightParking(true);
        state.SetCarParkType(CarParkType.Surface, true);
        state.SetCarParkType(CarParkType.Underground, true);

        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "Test",
            TestContext.Current.CancellationToken);

        Assert.Equal(VehicleType.HeavyVehicle, capturedVehicle);
        Assert.True(capturedAvailableOnly);
        Assert.True(capturedNightParking);
        Assert.Equal([CarParkType.Surface, CarParkType.Underground], capturedTypes);
        Assert.Equal(SearchOutcome.Results, state.Outcome);
        Assert.Single(state.Results);
    }

    [Fact]
    public async Task EmptySearchDistinguishesNearbyFromFilteredResults()
    {
        ParkingSearchState state = new(new FakeApiClient());

        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "Test",
            TestContext.Current.CancellationToken);
        Assert.Equal(SearchOutcome.NoNearbyCarParks, state.Outcome);

        state.SetAvailableOnly(true);
        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "Test",
            TestContext.Current.CancellationToken);
        Assert.Equal(SearchOutcome.NoFilteredResults, state.Outcome);
    }

    [Fact]
    public async Task ApiFailureKeepsLastSuccessfulResultsAndShowsError()
    {
        FakeApiClient apiClient = new()
        {
            Search = static (_, _, _, _, _, _) =>
                new CarParkSearchResponse { Results = [TestData.SearchResult()] },
        };
        ParkingSearchState state = new(apiClient);
        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "Test",
            TestContext.Current.CancellationToken);
        apiClient.Search = static (_, _, _, _, _, _) => throw new HttpRequestException();

        await state.SearchAsync(
            new MapCoordinate(1.32, 103.82),
            "Other",
            TestContext.Current.CancellationToken);

        Assert.Equal(SearchOutcome.ApiFailure, state.Outcome);
        Assert.Single(state.Results);
        Assert.Contains("temporarily unavailable", state.ErrorMessage);
    }

    [Fact]
    public void DeniedLocationCannotBeRequestedAgainInCircuit()
    {
        ParkingSearchState state = new(new FakeApiClient());

        Assert.True(state.BeginLocationRequest());
        state.SetLocationResult(new LocationResult(
            LocationResultStatus.Denied,
            null,
            "Location permission was denied."));

        Assert.False(state.BeginLocationRequest());
        Assert.Equal(LocationPermissionState.Denied, state.LocationState);
    }

    [Fact]
    public async Task SelectionLoadsAllDetailAndAlternatives()
    {
        CarParkSearchResult alternative = TestData.SearchResult("B2");
        FakeApiClient apiClient = new()
        {
            Detail = static (number, _) => TestData.Detail(number, 0, [TestData.SearchResult("B2")]),
        };
        ParkingSearchState state = new(apiClient);

        await state.SelectAsync("A1", TestContext.Current.CancellationToken);

        Assert.Equal("A1", state.SelectedCarParkNumber);
        Assert.NotNull(state.SelectedDetail);
        Assert.Equal(2, state.SelectedDetail.Availability.Lots.Count);
        Assert.Single(state.SelectedDetail.Alternatives);
        Assert.Equal(alternative.CarParkNumber, state.SelectedDetail.Alternatives.Single().CarParkNumber);
    }

    [Fact]
    public async Task SelectionPassesDestinationAndActiveFiltersToDetail()
    {
        double? latitude = null;
        double? longitude = null;
        bool? nightParking = null;
        IReadOnlyCollection<CarParkType> types = [];
        FakeApiClient apiClient = new()
        {
            DetailAsync = (number, _, capturedLatitude, capturedLongitude, capturedNightParking, capturedTypes, _) =>
            {
                latitude = capturedLatitude;
                longitude = capturedLongitude;
                nightParking = capturedNightParking;
                types = capturedTypes?.ToArray() ?? [];
                return Task.FromResult(TestData.Detail(number));
            },
        };
        ParkingSearchState state = new(apiClient);
        state.SetNightParking(true);
        state.SetCarParkType(CarParkType.Underground, true);
        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "Test",
            TestContext.Current.CancellationToken);

        await state.SelectAsync("A1", TestContext.Current.CancellationToken);

        Assert.Equal(1.31, latitude);
        Assert.Equal(103.81, longitude);
        Assert.True(nightParking);
        Assert.Equal([CarParkType.Underground], types);
    }

    [Fact]
    public async Task SupersededSearchCannotOverwriteNewerResults()
    {
        TaskCompletionSource<CarParkSearchResponse> first = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int callCount = 0;
        FakeApiClient apiClient = new()
        {
            SearchAsync = (_, _, _, _, _, _, _) =>
            {
                callCount++;
                return callCount == 1
                    ? first.Task
                    : Task.FromResult(new CarParkSearchResponse
                    {
                        Results = [TestData.SearchResult("NEW")]
                    });
            },
        };
        ParkingSearchState state = new(apiClient);
        Task firstSearch = state.SearchAsync(
            new MapCoordinate(1.30, 103.80),
            "Old",
            TestContext.Current.CancellationToken);
        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "New",
            TestContext.Current.CancellationToken);

        first.SetResult(new CarParkSearchResponse
        {
            Results = [TestData.SearchResult("OLD")]
        });
        await firstSearch;

        Assert.Equal("New", state.DestinationLabel);
        Assert.Equal("NEW", Assert.Single(state.Results).CarParkNumber);
    }

    [Fact]
    public async Task ClosingPendingDetailPreventsLateResponseFromReopeningDialog()
    {
        TaskCompletionSource<CarParkDetail> pending = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        FakeApiClient apiClient = new()
        {
            DetailAsync = (_, _, _, _, _, _, _) => pending.Task,
        };
        ParkingSearchState state = new(apiClient);

        Task selection = state.SelectAsync("A1", TestContext.Current.CancellationToken);
        state.CloseDetail();
        pending.SetResult(TestData.Detail());
        await selection;

        Assert.Null(state.SelectedDetail);
        Assert.False(state.IsLoadingDetail);
    }

    [Fact]
    public async Task DetailFailureUsesDedicatedVisibleErrorState()
    {
        FakeApiClient apiClient = new()
        {
            DetailAsync = static (_, _, _, _, _, _, _) =>
                Task.FromException<CarParkDetail>(new HttpRequestException()),
        };
        ParkingSearchState state = new(apiClient);

        await state.SelectAsync("A1", TestContext.Current.CancellationToken);

        Assert.Null(state.SelectedDetail);
        Assert.Contains("could not be loaded", state.DetailErrorMessage);
        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public async Task ManualRefreshUpdatesDataStatus()
    {
        FakeApiClient apiClient = new()
        {
            Status = static () => new DataStatus { Freshness = Freshness.Stale },
            RefreshStatus = static () => new DataStatus
            {
                Freshness = Freshness.Fresh,
                SourceUpdateTime = new DateTimeOffset(2026, 8, 12, 1, 1, 0, TimeSpan.Zero),
            },
        };
        ParkingSearchState state = new(apiClient);
        await state.LoadDataStatusAsync(TestContext.Current.CancellationToken);

        bool refreshed = await state.RefreshDataAsync(TestContext.Current.CancellationToken);

        Assert.True(refreshed);
        Assert.Equal(1, apiClient.RefreshCount);
        Assert.Equal(Freshness.Fresh, state.DataStatus?.Freshness);
        Assert.Equal("Live parking data refreshed.", state.DataRefreshMessage);
        Assert.False(state.IsRefreshingData);
    }

    [Fact]
    public async Task ManualRefreshCannotRestoreAnOlderSearchContext()
    {
        TaskCompletionSource<DataStatus> pendingRefresh = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        FakeApiClient apiClient = new()
        {
            RefreshStatusAsync = _ => pendingRefresh.Task,
        };
        ParkingSearchState state = new(apiClient);
        await state.SearchAsync(
            new MapCoordinate(1.30, 103.80),
            "Old",
            TestContext.Current.CancellationToken);
        Task<bool> refresh = state.RefreshDataAndCurrentSearchAsync(
            TestContext.Current.CancellationToken);

        await state.SearchAsync(
            new MapCoordinate(1.31, 103.81),
            "New",
            TestContext.Current.CancellationToken);
        pendingRefresh.SetResult(new DataStatus { Freshness = Freshness.Fresh });

        Assert.False(await refresh);
        Assert.Equal("New", state.DestinationLabel);
        Assert.Equal(new MapCoordinate(1.31, 103.81), state.Origin);
    }
}
