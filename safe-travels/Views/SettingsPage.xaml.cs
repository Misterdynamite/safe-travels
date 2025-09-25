namespace safe_travels.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }
    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        if (e.Value)
            ThemeHelper.SetTheme(AppTheme.Dark);
        else
            ThemeHelper.SetTheme(AppTheme.Light);
    }
}