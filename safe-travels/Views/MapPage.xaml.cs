using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Utilities;

namespace safe_travels.Views;

public partial class MapPage : ContentPage
{
    private List<Stop> _favoriteStops = new();

    public MapPage()
    {
        InitializeComponent();
        SetMapToCurrentLocationAsync();
        LoadDefaultStops(this, EventArgs.Empty);
        LoadFavoriteStops();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadFavoriteStops();
    }

    private void LoadFavoriteStops()
    {
        _favoriteStops = StorageManager.LoadFavoriteStops();
        FavoriteStopsCollection.ItemsSource = _favoriteStops;
    }

    private void OnFavoriteStopSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is Stop selectedStop)
        {
            // Get bus details using StopTripCalls
            var stopTripCalls = new StopTripsCalls();
            // Fetch trips asynchronously and navigate
            Device.BeginInvokeOnMainThread(async () =>
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
        // Deselect after navigation
        FavoriteStopsCollection.SelectedItem = null;
    }

    private async void SetMapToCurrentLocationAsync()
    {
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync();
            if (location == null)
            {
                location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
            }

            if (location != null)
            {
                StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(location.Latitude, location.Longitude),
                    Distance.FromKilometers(1)));
            }
            else
            {

                StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                 new Location(-36.75144113475619, 174.7286331813561),
                 Distance.FromKilometers(1)));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Location Error", ex.Message, "OK");
            // Move to default location on error
            StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(-36.75144113475619, 174.7286331813561),
                Distance.FromKilometers(1)));
        }
    }

    private async void LoadDefaultStops(object sender, EventArgs e)
    {
        try
        {
            // Assuming you have a MasterCaller instance available
            var masterCaller = new MasterCaller();
            var location = await Geolocation.GetLastKnownLocationAsync();
            if (location == null)
            {
                location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
            }
            double latitude = location?.Latitude ?? -36.75144113475619;
            double longitude = location?.Longitude ?? 174.7286331813561;


            var stops = await masterCaller.FetchStopsNearUser(latitude, longitude);

            if (stops != null && stops.Any())
            {
                foreach (var stop in stops)
                {
                    // Add pins to the map for each stop
                    var pin = new Pin
                    {
                        Label = stop.stopName,
                        Location = new Location(stop.stopLat, stop.stopLong),
                        Type = PinType.Place,
                        BindingContext = stop // Store the Stop object here
                    };
                    StopMap.Pins.Add(pin);

                    // Attach a handler to the pin's MarkerClicked event
                    pin.MarkerClicked += async (s, args) =>
                    {
                        if (s is Pin clickedPin && clickedPin.BindingContext is Stop clickedStop)
                        {
                            // Get bus details using StopTripCalls
                            var stopTripCalls = new StopTripsCalls();
                            List<TripStopResponse> busDetails = await stopTripCalls.GetTripsByStopID(clickedStop.stopId);

                            // Navigate to BusDetailPage, passing busDetails
                            // Pass busDetails, stopName, and stopId to BusDetailPage constructor
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
            }

            else
            {
                await DisplayAlert("No Stops Found", "No stops found for 'Constellation'.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnMarkerClicked(object sender, PinClickedEventArgs e)
    {
        if (sender is Pin pin && pin.MarkerId is string stopId)
        {
            Console.WriteLine($"StopID: {stopId}");
        }
    }
}