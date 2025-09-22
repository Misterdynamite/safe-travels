using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using safe_travels.API.AucklandTransportAPI;

namespace safe_travels.Views;

public partial class MapPage : ContentPage
{
    public MapPage()
    {
        InitializeComponent();
        LoadDefaultStops(this, EventArgs.Empty);
        SetMapToCurrentLocationAsync();
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

            //if (location != null)
            //{
            //    StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            //        new Location(location.Latitude, location.Longitude),
            //        Distance.FromKilometers(1)));
            //}
            //else
            //{
                // Default to specified coordinates if location is unavailable
                StopMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(-36.75144113475619, 174.7286331813561),
                    Distance.FromKilometers(1)));
            //}
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
            // Call DemoStopToTripFlow to get stops by name "Constellation"
            var stops = await masterCaller.DemoStopToTripFlow("Constellation");

            if (stops != null && stops.Any())
            {
                foreach (var stop in stops)
                {
                    // Add pins to the map for each stop
                    var pin = new Pin
                    {
                        Label = stop.attributes.stopName,
                        Location = new Location(stop.attributes.stopLat, stop.attributes.stopLong),
                        Type = PinType.Place
                    };
                    StopMap.Pins.Add(pin);
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
}