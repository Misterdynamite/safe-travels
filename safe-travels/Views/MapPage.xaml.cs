using Android.Content;
using Java.Nio.FileNio;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Utilities;
using safe_travels.Models;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace safe_travels.Views;

/// <summary>
/// Displays a map with bus stops, allows searching and proximity filtering, and manages favorite stops.
/// </summary>
public partial class MapPage : ContentPage
{
    /// <summary>
    /// Stores the user's favorite bus stops.
    /// </summary>
    private List<FavoriteStop> _favoriteStops = new();

    // Accessibility list and batching state
    private List<Stop> _accessibleStops = new();
    private int _currentStopBatch = 0;
    private const int StopsPerBatch = 10;
    private bool _isStopsListVisible = false;

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
        bool isAccessibilityMode = Preferences.Get("AccessibilityMode", false);

        NormalView.IsVisible = !isAccessibilityMode;
        AccessibilityView.IsVisible = isAccessibilityMode;

    }

    #region Favorite Stops

    /// <summary>
    /// Loads the user's favorite bus stops and updates the UI collection.
    /// </summary>
    private void LoadFavoriteStops()
    {
        _favoriteStops = FavoriteBusManager.LoadFavoriteStops();
        FavoriteStopsCollection.ItemsSource = _favoriteStops;

        // If an accessibility favorites list exists in XAML, populate it too so the AccessibilityView shows the same favorites
        var accessibleFavorites = FindByName("AccessibleFavoriteStops") as CollectionView;
        if (accessibleFavorites != null)
        {
            accessibleFavorites.ItemsSource = _favoriteStops;
        }
    }

    /// <summary>
    /// Handles selection of a favorite stop, opens its detail page.
    /// </summary>
    private async void OnFavoriteStopSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is FavoriteStop fav)
        {
            // Clear selections from both collections immediately to prevent stuck state
            var senderCollection = sender as CollectionView;
            if (senderCollection != null)
            {
                senderCollection.SelectedItem = null;
            }
            
            // Also clear the other favorites collection if it exists
            if (senderCollection == FavoriteStopsCollection)
            {
                var accessibleFavorites = FindByName("AccessibleFavoriteStops") as CollectionView;
                if (accessibleFavorites != null)
                {
                    accessibleFavorites.SelectedItem = null;
                }
            }
            else if (string.Equals(senderCollection?.StyleId, "AccessibleFavoriteStops", StringComparison.Ordinal))
            {
                FavoriteStopsCollection.SelectedItem = null;
            }

            var stopTripCalls = new InboundTripsAPI();
            var busDetails = await stopTripCalls.GetTripsByStopID(fav.stopId);

            if (Application.Current?.MainPage is NavigationPage navigationPage)
            {
                await navigationPage.Navigation.PushAsync(new BusDetailPage(busDetails, fav.stopName, fav.stopId));
            }
            else if (Navigation != null)
            {
                await Navigation.PushAsync(new BusDetailPage(busDetails, fav.stopName, fav.stopId));
            }
            else
            {
                await DisplayAlert("Navigation Error", "Navigation is not available. Please ensure this page is within a NavigationPage.", "OK");
            }
        }
    }

    /// <summary>
    /// Remove favorite when user taps the inline Remove button in the favorite item.
    /// </summary>
    private async void OnRemoveFavoriteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is FavoriteStop fav)
        {
            FavoriteBusManager.RemoveFavoriteStop(fav.stopId);
            LoadFavoriteStops();
            await DisplayAlert("Removed", $"Removed favorite '{fav.DisplayLabel}'", "OK");
        }
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
            ShowStopsInAccessibilityView(stops);
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
            await AddStopsToMap(stops);
            ShowStopsInAccessibilityView(stops);
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
    private async Task AddStopsToMap(IEnumerable<Stop> stops)
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

                    //pammis bit
                    if (IsTrainStop(clickedStop.stopName))
                    {
                       var flatTrips = busDetails.SelectMany(bd => bd.data).ToList();
                        await Navigation.PushAsync(new TrainDetailPage(flatTrips, clickedStop.stopId, clickedStop.stopName));
                    } else
                    {
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
                    //
                    
                }
            };
            await Task.Yield(); // Yield to keep UI responsive during pin addition
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
    
    private bool IsTrainStop(string stopName)
    {
        return stopName.Contains("Train", StringComparison.OrdinalIgnoreCase);
         }
    /// <summary>
    /// Handles nearby stops click events.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Pin clicked event arguments.</param>
    private void OnNearbyStopsButtonClicked(object sender, EventArgs e)
    {
        if (_isStopsListVisible)
        {
            AccessibleStopsList.IsVisible = false;
            ShowMoreButton.IsVisible = false;
            _isStopsListVisible = false;
            SemanticScreenReader.Announce("Nearby stops list hidden.");
            return;
        }

        _isStopsListVisible = true;
        AccessibleStopsList.IsVisible = true;
        StopModePicker.SelectedIndex = 0; // Nearby
        StopSearchBar.IsVisible = false;
        LoadStopsInProximity();
    }

    private void OnShowMoreClicked(object sender, EventArgs e)
    {
        ShowNextBatchOfStops();
    }

    private async Task OnMarkerClicked(object sender, PinClickedEventArgs e)
    {
        if ( sender is not Pin pin || pin.MarkerId is not string stopId)
            return;

        var context = BindingContext as dynamic;
        if (context?.Trips is not List<TripStopData> trips)
            return;

        string stopName = pin.Label ?? "";
        Console.WriteLine($"StopID: {stopId}");

       await Navigation.PushAsync(new BusDetailPage(trips, stopName, stopId));
        
    }

    /// <summary>
    /// Handles search button click events.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">Pin clicked event arguments.</param>
    private void OnSearchStopsButtonClicked(object sender, EventArgs e)
    {
        StopModePicker.SelectedIndex = 1; // Search mode
        StopSearchBar.IsVisible = true;

        // In accessibility mode, we can show the search bar or a prompt
        if (AccessibilityView.IsVisible)
        {
            AccessibleSearchSection.IsVisible = !AccessibleSearchSection.IsVisible;
            AccessibleSearchResults.IsVisible = false;

            if (AccessibleSearchSection.IsVisible)
                DisplayAlert("Search Mode", "Enter a stop name below to find bus stops.", "OK");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    private async void OnJourneyPlannerClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new JourneyPlannerPage());
    }



    #endregion

    #region Accessibility Mode
    private void ShowStopsInAccessibilityView(IEnumerable<Stop> stops)
    {
        if (AccessibilityView.IsVisible)
        {
            AccessibleStopsList.ItemsSource = stops.ToList();
        }
        _accessibleStops = stops.ToList();
        _currentStopBatch = 0;

        ShowNextBatchOfStops();
    }

    private void ShowNextBatchOfStops()
    {
        if (_accessibleStops == null || !_accessibleStops.Any())
        {
            AccessibleStopsList.ItemsSource = new List<Stop>();
            return;
        }

        int start = _currentStopBatch * StopsPerBatch;
        int end = Math.Min(start + StopsPerBatch, _accessibleStops.Count);

        var visibleBatch = _accessibleStops.Take(end).ToList();
        AccessibleStopsList.ItemsSource = visibleBatch;

        _currentStopBatch++;

        ShowMoreButton.IsVisible = _currentStopBatch * StopsPerBatch < _accessibleStops.Count;
    }

    private async void OnAccessibleStopSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is Stop selectedStop)
        {
            var stopTripCalls = new InboundTripsAPI();
            var busDetails = await stopTripCalls.GetTripsByStopID(selectedStop.stopId);
            await Navigation.PushAsync(new BusDetailPage(busDetails, selectedStop.stopName, selectedStop.stopId));
        }
        AccessibleStopsList.SelectedItem = null;
    }

    private async void OnAccessibleSearchCompleted(object sender, EventArgs e)
    {
        var query = AccessibleSearchEntry.Text?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        try
        {
            var stops = await AucklandTransportAPIClient.DemoStopToTripFlow(query);
            if (stops == null || !stops.Any())
            {
                await DisplayAlert("No Results", $"No stops found matching '{query}'.", "OK");
                AccessibleSearchResults.IsVisible = false;
                return;
            }

            AccessibleSearchResults.ItemsSource = stops.ToList();
            AccessibleSearchResults.IsVisible = true;

            SemanticScreenReader.Announce($"{stops.Count()} stops found.");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Search Error", ex.Message, "OK");
        }
    }

    private async void OnAccessibleSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is Stop selectedStop)
        {
            var stopTripCalls = new InboundTripsAPI();
            var busDetails = await stopTripCalls.GetTripsByStopID(selectedStop.stopId);
            await Navigation.PushAsync(new BusDetailPage(busDetails, selectedStop.stopName, selectedStop.stopId));
        }

        AccessibleSearchResults.SelectedItem = null;
    }
    #endregion
}