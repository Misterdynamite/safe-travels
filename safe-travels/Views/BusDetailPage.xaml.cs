using safe_travels.API.AucklandTransportAPI;

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

    //private async void OnMapButtonClicked(object sender, EventArgs e)
    //{
    //    await Navigation.PushAsync(new MapPage());
    //}
}