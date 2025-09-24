using safe_travels.API.AucklandTransportAPI;
using safe_travels.Utilities;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace safe_travels.Views;

public partial class BusDetailPage : ContentPage
{
    private string _stopName;
    private string _stopId;

    public BusDetailPage(List<TripStopResponse> trips, string stopName, string stopId)
    {
        InitializeComponent();
        _stopName = stopName;
        _stopId = stopId;
        BindingContext = new
        {
            StopName = stopName,
            StopId = stopId,
            Trips = trips.SelectMany(t => t.data).ToList()
        };

    }
    
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
        StorageManager.AddFavoriteStop(stop);
        DisplayAlert("Saved", $"Stop '{_stopName}' saved to favorites.", "OK");
    }

    public async Task FilterBusses(string stopHeadsign)
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

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string input = e.NewTextValue?.Trim();
        await FilterBusses(input);
    }

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
        var notify =  depTime - alert; 
        var delay = notify - DateTime.Now;

        if (delay.TotalMilliseconds <= 0)
        {
            return;
        }
        await Task.Delay(delay);
        await DisplayAlert("Reminder", $"Your bus departs at {departureTime} in 10 minutes.", "OK");

    }

    //private async void OnMapButtonClicked(object sender, EventArgs e)
    //{
    //    await Navigation.PushAsync(new MapPage());
    //}
}