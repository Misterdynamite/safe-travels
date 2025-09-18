namespace safe_travels.Views;

public partial class MapPage : ContentPage
{
	public MapPage()
	{
		InitializeComponent();
	}
    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}