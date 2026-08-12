using Bunit;
using CarparkAvailability.WebApp.Components.Pages;
using CarparkAvailability.WebApp.Components.Results;
using CarparkAvailability.WebApp.Generated;
using CarparkAvailability.WebApp.Services;
using CarparkAvailability.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace CarparkAvailability.WebApp.Tests;

public sealed class ComponentRenderingTests : BunitContext
{
    public ComponentRenderingTests()
    {
        Services.AddFluentUIComponents();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ResultCardShowsSourceUpdateTimeAndIsKeyboardOperable()
    {
        CarParkSearchResult result = TestData.SearchResult(freshness: Freshness.Stale, recommended: true);

        IRenderedComponent<ResultCard> component = Render<ResultCard>(parameters => parameters
            .Add(item => item.Result, result)
            .Add(item => item.VehicleType, VehicleType.Car)
            .Add(item => item.OnSelect, _ => Task.CompletedTask));

        AngleSharp.Dom.IElement button = component.Find("button.result-card");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Contains("View details for", button.GetAttribute("aria-label"));
        Assert.DoesNotContain("Stale data", component.Markup);
        Assert.DoesNotContain(">Fresh<", component.Markup);
        Assert.Contains("Updated", component.Markup);
        Assert.Contains("Recommended", component.Markup);
        Assert.Contains("distance", component.Markup);
        Assert.Contains("availability", component.Markup);
        Assert.Contains("occupancy", component.Markup);
    }

    [Fact]
    public void FullDetailShowsAlternativesAndAllReturnedLotTypes()
    {
        CarParkDetail detail = TestData.Detail(
            availableLots: 0,
            alternatives: [TestData.SearchResult("B2", 8)]);

        IRenderedComponent<CarParkDetailDialog> component = Render<CarParkDetailDialog>(parameters => parameters
            .Add(item => item.Detail, detail)
            .Add(item => item.VehicleType, VehicleType.Car)
            .Add(item => item.OnClose, () => Task.CompletedTask)
            .Add(item => item.OnSelectAlternative, _ => Task.CompletedTask));

        Assert.Contains("Nearby alternatives with verified availability", component.Markup);
        Assert.Contains("B2 TEST STREET", component.Markup);
        Assert.Contains("Car", component.Markup);
        Assert.Contains("Motorcycle", component.Markup);
        Assert.Contains("Height restriction", component.Markup);
        Assert.Contains("Free parking label", component.Markup);
    }

    [Fact]
    public void FullDetailExplainsWhenNoVerifiedAlternativeExists()
    {
        CarParkDetail detail = TestData.Detail(availableLots: 0);

        IRenderedComponent<CarParkDetailDialog> component = Render<CarParkDetailDialog>(parameters => parameters
            .Add(item => item.Detail, detail)
            .Add(item => item.VehicleType, VehicleType.Car)
            .Add(item => item.OnClose, () => Task.CompletedTask)
            .Add(item => item.OnSelectAlternative, _ => Task.CompletedTask));

        Assert.Contains("No verified alternative was found within 500 metres", component.Markup);
    }

    [Fact]
    public void DetailFailureIsPresentedInAccessibleDialog()
    {
        IRenderedComponent<CarParkDetailDialog> component = Render<CarParkDetailDialog>(parameters => parameters
            .Add(item => item.ErrorMessage, "Car park details could not be loaded.")
            .Add(item => item.VehicleType, VehicleType.Car)
            .Add(item => item.OnClose, () => Task.CompletedTask)
            .Add(item => item.OnSelectAlternative, _ => Task.CompletedTask));

        Assert.Contains("Details could not be loaded", component.Markup);
        Assert.Equal(
            "Car park details could not be loaded.",
            component.Find("[role='alert']").TextContent);
    }

    [Fact]
    public void AvailabilityTimesAreRenderedInSingaporeTime()
    {
        CarParkSearchResult result = TestData.SearchResult();
        result.Availability.SourceUpdateTime = new DateTimeOffset(
            2026,
            8,
            12,
            1,
            0,
            0,
            TimeSpan.Zero);

        IRenderedComponent<ResultCard> component = Render<ResultCard>(parameters => parameters
            .Add(item => item.Result, result)
            .Add(item => item.VehicleType, VehicleType.Car)
            .Add(item => item.OnSelect, _ => Task.CompletedTask));

        Assert.Contains("12 Aug, 9:00 AM SGT", component.Markup);
    }

    [Fact]
    public void StaleDataStatusOffersManualRefresh()
    {
        int refreshCount = 0;
        DataStatus status = new()
        {
            Freshness = Freshness.Stale,
            SourceUpdateTime = new DateTimeOffset(2026, 8, 12, 1, 0, 0, TimeSpan.Zero),
        };
        IRenderedComponent<DataStatusSummary> component = Render<DataStatusSummary>(parameters => parameters
            .Add(item => item.Status, status)
            .Add(item => item.OnRefresh, () => refreshCount++));

        AngleSharp.Dom.IElement refreshButton = component.Find("fluent-button");
        Assert.Contains("Refresh data", refreshButton.TextContent);
        Assert.Equal("accent", refreshButton.GetAttribute("appearance"));
        Assert.DoesNotContain("Parking data is stale", component.Markup);
        Assert.Contains("Latest aggregate update: 12 Aug, 9:00 AM SGT", component.Markup);
        refreshButton.Click();
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public void FreshDataStatusShowsDisabledManualRefresh()
    {
        IRenderedComponent<DataStatusSummary> component = Render<DataStatusSummary>(parameters => parameters
            .Add(item => item.Status, new DataStatus { Freshness = Freshness.Fresh }));

        AngleSharp.Dom.IElement refreshButton = component.Find("fluent-button");
        Assert.Contains("Refresh data", refreshButton.TextContent);
        Assert.Equal("accent", refreshButton.GetAttribute("appearance"));
        Assert.True(refreshButton.HasAttribute("disabled"));
        Assert.DoesNotContain("Live parking data is fresh", component.Markup);
    }

    [Fact]
    public void MainPageHasAccessibleSearchFiltersMapAndListAlternative()
    {
        FakeApiClient apiClient = new();
        FakeMapInterop mapInterop = new();
        Services.AddSingleton<ICarparkAvailabilityApiClient>(apiClient);
        Services.AddSingleton<IMapInterop>(mapInterop);
        Services.AddSingleton<ParkingSearchState>();

        IRenderedComponent<Home> component = Render<Home>();

        Assert.NotNull(component.Find("h1#page-heading"));
        Assert.NotNull(component.Find("label[for='vehicle-type']"));
        Assert.NotNull(component.Find("[aria-label='Car parks; accessible alternative to the map']"));
        Assert.NotNull(component.Find("[aria-label='Map of nearby car parks']"));
        Assert.NotNull(component.Find("[role='status'][aria-live='polite']"));
        Assert.Contains("Use current location", component.Markup);
        Assert.Contains("workspace mobile-map", component.Markup);
        AngleSharp.Dom.IElement mapButton = component
            .FindAll("fluent-button")
            .Single(button => button.TextContent.Trim() == "Map");
        Assert.Equal("accent", mapButton.GetAttribute("appearance"));
    }

    [Fact]
    public void ExplicitLocationActionUsesMapAbstractionAndRendersResults()
    {
        FakeApiClient apiClient = new()
        {
            Search = static (_, _, _, _, _, _) =>
                new CarParkSearchResponse { Results = [TestData.SearchResult()] },
        };
        FakeMapInterop mapInterop = new()
        {
            LocationResult = new LocationResult(
                LocationResultStatus.Granted,
                new MapCoordinate(1.31, 103.81),
                "Using your current location for this circuit only."),
        };
        Services.AddSingleton<ICarparkAvailabilityApiClient>(apiClient);
        Services.AddSingleton<IMapInterop>(mapInterop);
        Services.AddSingleton<ParkingSearchState>();
        IRenderedComponent<Home> component = Render<Home>();

        AngleSharp.Dom.IElement locationButton = component
            .FindAll("fluent-button")
            .Single(button => button.TextContent.Contains("Use current location", StringComparison.Ordinal));
        locationButton.Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal(1, mapInterop.LocationRequestCount);
            Assert.Single(mapInterop.Markers);
            Assert.Equal("Current location", mapInterop.OriginLabel);
            Assert.Contains("A1 TEST STREET", component.Markup);
        });
    }

    [Fact]
    public void DestinationMarkerUsesEnteredPlaceNameInsteadOfResolvedAddress()
    {
        FakeApiClient apiClient = new()
        {
            Search = static (_, _, _, _, _, _) =>
                new CarParkSearchResponse { Results = [TestData.SearchResult()] },
        };
        FakeMapInterop mapInterop = new()
        {
            GeocodeResult = new GeocodeResult(
                GeocodeResultStatus.Resolved,
                new MapCoordinate(1.28, 103.85),
                "182 Cecil Street, Singapore 069547",
                null),
        };
        ParkingSearchState state = new(apiClient)
        {
            SearchText = "Frasers Tower",
        };
        Services.AddSingleton<ICarparkAvailabilityApiClient>(apiClient);
        Services.AddSingleton<IMapInterop>(mapInterop);
        Services.AddSingleton(state);
        IRenderedComponent<Home> component = Render<Home>();

        component.Find("form.search-panel").Submit();

        component.WaitForAssertion(() =>
        {
            Assert.Equal("Frasers Tower", mapInterop.OriginLabel);
            Assert.Equal("182 Cecil Street, Singapore 069547", state.DestinationLabel);
        });
    }

    [Fact]
    public void AmbiguousDestinationShowsSelectableMatchesUnderInput()
    {
        FakeApiClient apiClient = new()
        {
            Search = static (_, _, _, _, _, _) =>
                new CarParkSearchResponse { Results = [TestData.SearchResult()] },
        };
        FakeMapInterop mapInterop = new()
        {
            GeocodeResult = new GeocodeResult(
                GeocodeResultStatus.Ambiguous,
                null,
                null,
                null,
                [
                    new GeocodeCandidate(
                        "JUMBO Seafood - Riverside Point",
                        "30 Merchant Road, Singapore",
                        new MapCoordinate(1.29, 103.84)),
                    new GeocodeCandidate(
                        "JUMBO Seafood - East Coast Seafood Centre",
                        "1206 East Coast Parkway, Singapore",
                        new MapCoordinate(1.30, 103.93)),
                ]),
        };
        ParkingSearchState state = new(apiClient)
        {
            SearchText = "JUMBO Seafood",
        };
        Services.AddSingleton<ICarparkAvailabilityApiClient>(apiClient);
        Services.AddSingleton<IMapInterop>(mapInterop);
        Services.AddSingleton(state);
        IRenderedComponent<Home> component = Render<Home>();

        component.Find("form.search-panel").Submit();

        component.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement options = component.Find(".destination-options");
            Assert.Contains("Riverside Point", options.TextContent);
            Assert.Contains("East Coast Seafood Centre", options.TextContent);
            Assert.DoesNotContain("Choose a more specific destination", component.Markup);
        });

        component.FindAll(".destination-options button")[1].Click();

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindAll(".destination-options"));
            Assert.Equal("JUMBO Seafood - East Coast Seafood Centre", state.SearchText);
            Assert.Equal("JUMBO Seafood - East Coast Seafood Centre", mapInterop.OriginLabel);
            Assert.Equal("1206 East Coast Parkway, Singapore", state.DestinationLabel);
            Assert.Single(state.Results);
        });
    }

    [Fact]
    public void SearchingVisibleMapAreaUpdatesDestinationMarkerTooltip()
    {
        FakeApiClient apiClient = new()
        {
            Search = static (_, _, _, _, _, _) =>
                new CarParkSearchResponse { Results = [TestData.SearchResult()] },
        };
        FakeMapInterop mapInterop = new();
        ParkingSearchState state = new(apiClient)
        {
            SearchText = "Frasers Tower",
        };
        Services.AddSingleton<ICarparkAvailabilityApiClient>(apiClient);
        Services.AddSingleton<IMapInterop>(mapInterop);
        Services.AddSingleton(state);
        IRenderedComponent<Home> component = Render<Home>();
        component.Find("form.search-panel").Submit();
        component.WaitForAssertion(() => Assert.Equal("Frasers Tower", mapInterop.OriginLabel));

        component.InvokeAsync(() => component.Instance.OnMapViewportChanged(1.31, 103.82));
        component.WaitForAssertion(() => Assert.NotNull(state.PendingViewport));
        component.FindAll("fluent-button")
            .Single(button => button.TextContent.Contains("Search this map area", StringComparison.Ordinal))
            .Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal("Marina Bay Sands", state.SearchText);
            Assert.Equal("Marina Bay Sands", mapInterop.OriginLabel);
            Assert.Equal("10 Bayfront Avenue, Singapore 018956", state.DestinationLabel);
            Assert.Equal(new MapCoordinate(1.31, 103.82), state.Origin);
        });
    }

    [Fact]
    public void CurrentLocationOutsideSingaporeShowsModalWithoutSearching()
    {
        int searchCount = 0;
        FakeApiClient apiClient = new()
        {
            Search = (_, _, _, _, _, _) =>
            {
                searchCount++;
                return new CarParkSearchResponse();
            },
        };
        FakeMapInterop mapInterop = new()
        {
            LocationResult = new LocationResult(
                LocationResultStatus.Granted,
                new MapCoordinate(35.68, 139.76),
                "Using your current location for this circuit only."),
        };
        Services.AddSingleton<ICarparkAvailabilityApiClient>(apiClient);
        Services.AddSingleton<IMapInterop>(mapInterop);
        Services.AddSingleton<ParkingSearchState>();
        IRenderedComponent<Home> component = Render<Home>();

        AngleSharp.Dom.IElement locationButton = component
            .FindAll("fluent-button")
            .Single(button => button.TextContent.Contains("Use current location", StringComparison.Ordinal));
        locationButton.Click();

        component.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement dialog = component.Find("fluent-dialog[aria-label='Current location outside Singapore']");
            Assert.False(dialog.HasAttribute("hidden"));
            AngleSharp.Dom.IElement alert = component.Find("[role='alert']");
            Assert.Contains("searches only for HDB car parks in Singapore", alert.TextContent);
            Assert.Equal(0, searchCount);
        });

        component.FindAll("fluent-button")
            .Single(button => button.TextContent.Contains("Got it", StringComparison.Ordinal))
            .Click();

        component.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement dialog = component.Find("fluent-dialog[aria-label='Current location outside Singapore']");
            Assert.True(dialog.HasAttribute("hidden"));
        });
    }
}
