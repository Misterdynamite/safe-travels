using safe_travels.API.AucklandTransportAPI;
using safe_travels.Utilities;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace safe_travels.Views;

/// <summary>
/// Displays details for a specific bus stop, including upcoming trips and favorite management.
/// </summary>
public partial class BusDetailPage : ContentPage
{
    private string _stopName;
    private string _stopId;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusDetailPage"/> class.
    /// </summary>
    /// <param name="trips">A list of trip stop responses containing trip data for the stop.</param>
    /// <param name="stopName">The name of the bus stop.</param>
    /// <param name="stopId">The unique identifier of the bus stop.</param>
    public BusDetailPage(List<TripStopResponse> trips, string stopName, string stopId)
    {
        InitializeComponent();

        // Debug print for each trip
        //foreach (var tripResponse in trips)
        //{
        //    foreach (var trip in tripResponse.data)
        //    {
        //        System.Diagnostics.Debug.WriteLine(trip);
        //    }
        //}

        _stopName = stopName;
        _stopId = stopId;
        BindingContext = new
        {
            StopName = stopName,
            StopId = stopId,
            Trips = trips.SelectMany(t => t.data).ToList()
        };
    }

    /// <summary>
    /// Handles the event when the user clicks to save the stop as a favorite.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void OnSaveStopClicked(object sender, EventArgs e)
    {
        // Create a Stop object and save as favorite
        var stop = new Stop
        {
            stopId = _stopId,
            stopName = _stopName,
            id = _stopId, // Assuming id should match stopId
            type = "stop" // Assuming type is always "stop"
            // Add other properties if needed
        };
        FavoriteBusManager.AddFavoriteStop(stop);
        DisplayAlert("Saved", $"Stop '{_stopName}' saved to favorites.", "OK");
    }

    /// <summary>
    /// Filters the displayed buses by the provided headsign string.
    /// </summary>
    /// <param name="stopHeadsign">The headsign to filter by. If null or empty, all buses are shown.</param>
    public void FilterBuses(string stopHeadsign)
    {
        //cast bindingcontext ot a dynamic to access trips
        var context = (dynamic)BindingContext;
        var trips = (List<TripStopData>)context.Trips;

        if (trips == null)
            return;

        // filter by headsign using a case-insensitive partial match
        var filtered = string.IsNullOrEmpty(stopHeadsign)
            ? trips
            : trips.Where(bus =>
                bus.attributes != null &&
                !string.IsNullOrEmpty(bus.attributes.stopHeadSign) &&
                bus.attributes.stopHeadSign.Contains(stopHeadsign, StringComparison.OrdinalIgnoreCase)
            ).ToList();

        BusCollection.ItemsSource = filtered;
    }

    /// <summary>
    /// Handles the event when the search text changes, updating the bus filter.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The text changed event arguments.</param>
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string input = e.NewTextValue?.Trim();
        FilterBuses(input);
    }

    /// <summary>
    /// Schedules a notification to alert the user 5 minutes before the specified departure time.
    /// </summary>
    /// <param name="departureTime">The departure time as a string.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task departureNotification(string departureTime)
    {
        //parse dateTime
        if (!DateTime.TryParse(departureTime, out DateTime depTime))
        {
            await DisplayAlert("Error", "Invalid departure time format.", "OK");
            return;
        }
        //alerts user 5 minutes before departure
        var alert = TimeSpan.FromMinutes(5);
        var notify = depTime - alert;
        var delay = notify - DateTime.Now;

        if (delay.TotalMilliseconds <= 0)
        {
            return;
        }
        await Task.Delay(delay);
        await DisplayAlert("Reminder", $"Your bus departs at {departureTime} in 10 minutes.", "OK");
    }

    /// <summary>
    /// Handles selection of a bus in the list, shows picker for notification time.
    /// </summary>
    private async void OnBusSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is TripStopData selectedBus)
        {
            string[] options = { "1 minute", "3 minutes", "5 minutes", "10 minutes" };
            string chosen = await DisplayActionSheet(
                $"Set alert for bus to '{selectedBus.attributes.stopHeadSign}'?",
                "Cancel", null, options);
            int minutes = chosen switch
            {
                "1 minute" => 1,
                "3 minutes" => 3,
                "5 minutes" => 5,
                "10 minutes" => 10,
                _ => 0
            };
            if (minutes > 0)
            {
                await ScheduleBusArrivalNotification(selectedBus, minutes);
            }
        }
        BusCollection.SelectedItem = null;
    }

    /// <summary>
    /// Schedules a notification for the selected bus arrival time minus the chosen minutes.
    /// </summary>
    private async Task ScheduleBusArrivalNotification(TripStopData bus, int minutesBefore)
    {
        string arrivalTime = bus.attributes.arrivalTime;
        if (!DateTime.TryParse(arrivalTime, out DateTime arrTime))
        {
            await DisplayAlert("Error", "Invalid arrival time format.", "OK");
            return;
        }
        var notifyTime = arrTime - TimeSpan.FromMinutes(minutesBefore);
        var delay = notifyTime - DateTime.Now;
        if (delay.TotalMilliseconds <= 0)
        {
            await DisplayAlert("Too Late", $"Bus to '{bus.attributes.stopHeadSign}' arrives in less than {minutesBefore} minutes.", "OK");
            return;
        }
        await DisplayAlert("Alert Set", $"You will be notified {minutesBefore} minutes before arrival.", "OK");
        await Task.Delay(delay);
        await DisplayAlert("Bus Arriving Soon", $"Bus to '{bus.attributes.stopHeadSign}' arrives in {minutesBefore} minutes.", "OK");
    }
}