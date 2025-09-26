namespace safe_travels;
using Microsoft.Maui;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Views;
using Microsoft.Maui.Storage;


public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Persist and apply theme on app load
        const string ThemePrefKey = "AppTheme";
        var theme = Preferences.Get(ThemePrefKey, "Light");
        if (theme == "Dark")
        {
            ThemeHelper.SetTheme(AppTheme.Dark);
        }
        else
        {
            ThemeHelper.SetTheme(AppTheme.Light);
        }

        // Wrap your starting page in a NavigationPage
        MainPage = new NavigationPage(new MapPage());
    }
}