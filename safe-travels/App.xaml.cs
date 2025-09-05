namespace safe_travels;
using Microsoft.Maui;
using safe_travels.Views;


public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // Removed obsolete MainPage assignment
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Set the root page here instead of using MainPage
        return new Window(new MainPage());
    }
}