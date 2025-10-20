using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Utilities;

namespace safe_travels.Views;

/// <summary>
/// Displays a map with bus stops, allows searching and proximity filtering, and manages favorite stops.
/// </summary>
public partial class MapPage : ContentPage
{
    /// <summary>
    /// Stores the user's favorite bus stops.
    /// </summary>
    private List<Stop> _favoriteStops = new();

    /// <summary>
    /// Mode for showing stops near the user's location.
    /// </summary>
    private const int MODE_PROXIMITY = 0;

    /// <summary>
    /// Mode for searching stops by name.
    /// </summary>
    private const int MODE_SEARCH = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="MapPage"/> class.
    /// Sets up the map, loads favorite stops, and displays stops in proximity.
    /// </summary>
    public MapPage()
    {
        InitializeComponent();
        StopModePicker.SelectedIndex = MODE_PROXIMITY;
        SetMapToCurrentLocationAsync();
        LoadStopsInProximity();
        LoadFavoriteStops();
    }

    /// <summary>
    /// Called when the page appears. Reloads favorite stops.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadFavoriteStops();
    }

    #region Favorite Stops

    /// <summary>
    /// Loads the user's favorite bus stops and updates the UI collection.
    /// </summary>
    private void LoadFavoriteStops()
    {
        _favoriteStops = FavoriteBusManager.LoadFavoriteStops();
        FavoriteStopsCollection.ItemsSource = _favoriteStops;
    }

    /// <summary>
    /// Handles selection of a favorite stop, navigates to its detail page.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Selection changed event arguments.</param>
    private void OnFavoriteStopSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is Stop selectedStop)
        {
            var stopTripCalls = new InboundTripsAPI();
            Dispatcher.Dispatch(async () =>
            {
                var busDetails = await stopTripCalls.GetTripsByStopID(selectedStop.stopId);
                if (Application.Current?.MainPage is NavigationPage navigationPage)
                {
                    await navigationPage.Navigation.PushAsync(new BusDetailPage(busDetails, selectedStop.stopName, selectedStop.stopId));
                }
                else if (Navigation != null)
                {
                    await Navigation.PushAsync(new BusDetailPage(busDetails, selectedStop.stopName, selectedStop.stopId));
                }
                else
                {
                    await DisplayAlert("Navigation Error", "Navigation is not available. Please ensure this page is within a NavigationPage.", "OK");
                }
            });
        }
        FavoriteStopsCollection.SelectedItem = null;
    }
    #endregion

    #region Map Setup

    /// <summary>
    /// Sets the map view to the user's current location, or a default location if unavailable.
    /// </summary>
    private async void SetMapToCurrentLocationAsync()
    {
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync();
            if (location == null)
            {
                location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
            }

            var regionLocation = location != null
                ? new Location(location.Latitude, location.Longitude)
                : new Location(-36.75144113475619, 174.7286331813561);

            StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(regionLocation, Distance.FromKilometers(1)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Location Error", ex.Message, "OK");
            StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(-36.75144113475619, 174.7286331813561),
                Distance.FromKilometers(1)));
        }
    }
    #endregion

    #region Stops Loading & Map Pins

    /// <summary>
    /// Loads bus stops near the user's location and adds them as pins to the map.
    /// </summary>
    private async void LoadStopsInProximity()
    {
        ClearMapPins();
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync();
            if (location == null)
            {
                location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
            }
            double latitude = location?.Latitude ?? -36.75144113475619;
            double longitude = location?.Longitude ?? 174.7286331813561;

            var stops = await AucklandTransportAPIClient.FetchStopsNearUser(latitude, longitude);
            AddStopsToMap(stops);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Loads bus stops by name and adds them as pins to the map.
    /// </summary>
    /// <param name="stopName">The name of the stop to search for.</param>
    private async void LoadStopsByName(string stopName)
    {
        ClearMapPins();
        try
        {
            var stops = await AucklandTransportAPIClient.DemoStopToTripFlow(stopName);
            AddStopsToMap(stops);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Adds a collection of stops as pins to the map and centers the map view.
    /// </summary>
    /// <param name="stops">The stops to add to the map.</param>
    private void AddStopsToMap(IEnumerable<Stop> stops)
    {
        if (stops == null || !stops.Any()) return;

        // Add pins
        foreach (var stop in stops)
        {
            var pin = new Pin
            {
                Label = stop.stopName,
                Location = new Location(stop.stopLat, stop.stopLong),
                Type = PinType.Place,
                BindingContext = stop
            };

            StopMap.Pins.Add(pin);

            pin.MarkerClicked += async (s, args) =>
            {
                if (s is Pin clickedPin && clickedPin.BindingContext is Stop clickedStop)
                {
                    var stopTripCalls = new InboundTripsAPI();
                    var busDetails = await stopTripCalls.GetTripsByStopID(clickedStop.stopId);
                    if (Application.Current?.MainPage is NavigationPage navigationPage)
                    {
                        await navigationPage.Navigation.PushAsync(new BusDetailPage(busDetails, clickedStop.stopName, clickedStop.stopId));
                    }
                    else if (Navigation != null)
                    {
                        await Navigation.PushAsync(new BusDetailPage(busDetails, clickedStop.stopName, clickedStop.stopId));
                    }
                    else
                    {
                        await DisplayAlert("Navigation Error", "Navigation is not available. Please ensure this page is within a NavigationPage.", "OK");
                    }
                }
            };
        }

        // Center map between all stops
        double avgLat = stops.Average(s => s.stopLat);
        double avgLong = stops.Average(s => s.stopLong);
        var center = new Location(avgLat, avgLong);

        // Calculate max distance from center to any stop for radius
        double maxDistance = stops.Max(s =>
            Distance.BetweenPositions(center, new Location(s.stopLat, s.stopLong)).Kilometers);
        double radius = Math.Max(1, maxDistance); // Minimum 1km radius

        StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radius)));
    }

    /// <summary>
    /// Removes all pins from the map.
    /// </summary>
    private void ClearMapPins()
    {
        StopMap.Pins.Clear();
    }
    #endregion

    #region UI Events

    /// <summary>
    /// Handles changes to the stop mode picker, toggling between proximity and search modes.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Event arguments.</param>
    private void OnStopModeChanged(object sender, EventArgs e)
    {
        if (StopModePicker.SelectedIndex == MODE_PROXIMITY)
        {
            StopSearchBar.IsVisible = false;
            LoadStopsInProximity();
        }
        else if (StopModePicker.SelectedIndex == MODE_SEARCH)
        {
            StopSearchBar.IsVisible = true;
            ClearMapPins();
        }
    }

    /// <summary>
    /// Handles the search event for stops by name.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Event arguments.</param>
    private void OnStopSearch(object sender, EventArgs e)
    {
        var searchText = StopSearchBar.Text?.Trim();
        if (!string.IsNullOrEmpty(searchText))
        {
            LoadStopsByName(searchText);
        }
    }
    #endregion

    #region Navigation & Misc

    /// <summary>
    /// Handles the back button click event, navigating to the previous page.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    /// <summary>
    /// Handles marker click events on the map pins.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Pin clicked event arguments.</param>
    private void OnMarkerClicked(object sender, PinClickedEventArgs e)
    {
        if (sender is Pin pin && pin.MarkerId is string stopId)
        {
            Console.WriteLine($"StopID: {stopId}");
        }
    }


    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    #endregion

}