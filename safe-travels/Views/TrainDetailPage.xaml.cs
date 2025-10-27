using Microsoft.Maui.Controls;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Models;
using safe_travels.Utilities;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace safe_travels.Views;
public partial class TrainDetailPage : ContentPage
{
    string _stopId;
    string _stopName;


    public TrainDetailPage(List<TripStopData> trips, string stopId, string stopName)
    {

        InitializeComponent();
        StopNameLabel.Text = stopName;
        StopIdLabel.Text = $"Stop ID: {stopId}";
        TrainCollection.ItemsSource = trips;
        Console.WriteLine($"TrainDetailPage loaded for StopID: {stopId}, StopName: {stopName}");

    }


    private async void onTrainSelected(object sender, SelectionChangedEventArgs e)
    {

        TrainStopsCollection.ItemsSource = trips;

        var selectedTrip = e.CurrentSelection.FirstOrDefault() as TripStopData;
        if (selectedTrip == null || selectedTrip.attributes == null)
            return;

        _stopId = selectedTrip.attributes.stopId;
        _stopName = selectedTrip.attributes.stopHeadSign;

            // Get service alerts for the stop (with automatic fallback)
            var alerts = await AucklandTransportAPIClient.GetServiceAlertsForStop(selectedTrip.attributes.stopId);

            // Check if the service is running
            bool isRunning = AucklandTransportAPIClient.IsServiceRunning(
                selectedTrip.attributes,
                alerts,
                DateTime.Now,
                "AT"
            );

            await DisplayAlert("Service Status",
                isRunning ? "This train is currently running." : "This train is not in service.",
                "OK");
        }
    
    


        private void OnSaveStopClicked(object sender, EventArgs e)
    {

        var stop = new Models.FavoriteStop
        {
            stopId = _stopId,
            stopName = _stopName,
            id = _stopId,
            type = "stop"
        };

        FavoriteBusManager.AddFavoriteStop(stop);
        DisplayAlert("Saved", $"Stop '{_stopName}' saved to favorites.", "OK");
    }

    private List<TripStopData> _allTrips = new();
    private List<TripStopResponse> trips;

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = e.NewTextValue?.ToLower() ?? "";

        var filtered = _allTrips
            .Where(t => t.attributes.stopHeadSign.ToLower().Contains(query)
                     || t.attributes.routeId.ToLower().Contains(query))
            .ToList();

        TrainCollection.ItemsSource = filtered;
    }

}









