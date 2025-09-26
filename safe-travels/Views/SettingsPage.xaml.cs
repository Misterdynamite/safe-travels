using Microsoft.Maui.Storage;

namespace safe_travels.Views;

public partial class SettingsPage : ContentPage
{
    private const string ThemePrefKey = "AppTheme";
    public SettingsPage()
    {
        InitializeComponent();
        // Load theme from preferences
        var theme = Preferences.Get(ThemePrefKey, "Light");
        if (theme == "Dark")
        {
            ThemeHelper.SetTheme(AppTheme.Dark);
            ThemeSwitch.IsToggled = true;
        }
        else
        {
            ThemeHelper.SetTheme(AppTheme.Light);
            ThemeSwitch.IsToggled = false;
        }
    }
    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        if (e.Value)
        {
            ThemeHelper.SetTheme(AppTheme.Dark);
            Preferences.Set(ThemePrefKey, "Dark");
        }
        else
        {
            ThemeHelper.SetTheme(AppTheme.Light);
            Preferences.Set(ThemePrefKey, "Light");
        }
    }
}