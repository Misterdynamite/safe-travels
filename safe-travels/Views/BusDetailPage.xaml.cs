using safe_travels.API.AucklandTransportAPI;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace safe_travels.Views;

public partial class BusDetailPage : ContentPage
{
    public BusDetailPage(List<TripStopResponse> trips, string stopName, string stopId)
    {
        InitializeComponent();
        BindingContext = new
        {
            StopName = stopName,
            StopId = stopId,
            Trips = trips.SelectMany(t => t.data).ToList()
        };

    }
    
    public async Task FilterBusses(string stopHeadsign)
    {
        //cast bindingcontext ot a dynamic to access trips
        var context = (dynamic)BindingContext;
        var trips = (List<TripStopData>)context.Trips;

        if (trips == null)
            return;

        // filter by headsign using the correct property path
        var filtered = string.IsNullOrEmpty(stopHeadsign)
            ? trips
            : trips.Where(bus => bus.attributes != null && bus.attributes.stopHeadSign == stopHeadsign).ToList();
            
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