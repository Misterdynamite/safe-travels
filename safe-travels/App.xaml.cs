namespace safe_travels;
using Microsoft.Maui;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Views;


public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Wrap your starting page in a NavigationPage
        MainPage = new NavigationPage(new MapPage());
    }
}