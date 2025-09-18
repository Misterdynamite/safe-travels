namespace safe_travels.Views;

<<<<<<< Updated upstream
public partial class NewPage1 : ContentPage
{
	public NewPage1()
	{
		InitializeComponent();
	}
=======
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
>>>>>>> Stashed changes
}