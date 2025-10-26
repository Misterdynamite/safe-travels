namespace safe_travels.Utilities;

public static class ThemeHelper
{
    public static void SetTheme(AppTheme theme)
    {
        Application.Current.UserAppTheme = theme;
    }
}