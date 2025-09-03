namespace safe_travels.Views;
using Microsoft.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new JourneyPlannerPage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}