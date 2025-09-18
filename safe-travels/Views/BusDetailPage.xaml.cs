namespace safe_travels.Views;

public partial class BusDetailPage : ContentPage
{
	public BusDetailPage()
	{
		InitializeComponent();
	}
    private async void OnMapButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MapPage());
    }
}