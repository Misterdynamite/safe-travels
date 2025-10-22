using Microsoft.Maui.Storage;

namespace safe_travels.Views;

public partial class SettingsPage : ContentPage
{
    private const string ThemePrefKey = "AppTheme";
    private const string AccessibilityModePrefKey = "AccessibilityMode";
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

        bool IsAccessibilityMode = Preferences.Get(AccessibilityModePrefKey, false);
        AppSettings.IsAccessibilityMode = IsAccessibilityMode;
        AccessibilitySwitch.IsToggled = IsAccessibilityMode;
        UpdateAccessibilityLabel(IsAccessibilityMode);
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

    private void OnAccessibilityToggled(object sender, ToggledEventArgs e)
    {
        AppSettings.IsAccessibilityMode = e.Value;
        Preferences.Set(AccessibilityModePrefKey, e.Value);
        UpdateAccessibilityLabel(e.Value);
        string announcement = e.Value
            ? "Accessibility mode enabled. Visual map hidden."
            : "Accessibility mode disabled. Visual elements restored.";

        SemanticScreenReader.Announce(announcement);
        DisplayAlert("Accessibility", announcement, "OK");
    }

    private void UpdateAccessibilityLabel(bool isEnabled)
    {
        AccessibilityStatusLabel.Text = isEnabled
            ? "Accessibility Mode is ON"
            : "Accessibility Mode is OFF";
        AccessibilityStatusLabel.TextColor = isEnabled ? Colors.Green : Colors.Gray;
        SemanticScreenReader.Announce(AccessibilityStatusLabel.Text);
    }
}