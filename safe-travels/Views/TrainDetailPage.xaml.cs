using API_Tester.AucklandTransportAPI;
using Microsoft.Maui.Controls;
using safe_travels.API.AucklandTransportAPI;
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


        var selectedTrip = e.CurrentSelection.FirstOrDefault() as TripStopData;
        _stopId = selectedTrip.attributes.stopId;
        _stopName = selectedTrip.attributes.stopHeadSign;

        if (selectedTrip == null || selectedTrip.attributes == null)
            return;

        string stopName = selectedTrip.attributes.stopHeadSign;
        {
            if (!stopName.Contains("Train Station", StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("Not a Train Station", "This stop is not a train station.", "OK");
                return;

            }


            var service = new ServiceUpdates();
            var alerts = await service.GetLegacyServiceAlertsAsync(selectedTrip.attributes.stopId);

            bool isRunning = service.isTrainRunning(
                selectedTrip.attributes,
                alerts,
                DateTime.Now,
                "AT"
            );

            await DisplayAlert("Service Status",
                isRunning ? "This bus is currently running." : "This bus is not in service.",
                "OK");
            await Navigation.PushAsync(new TrainDetailPage(new List<TripStopData> { selectedTrip }, selectedTrip.attributes.stopId, ""));
        }
    }


        private void OnSaveStopClicked(object sender, EventArgs e)
    {

        var stop = new Stop
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





