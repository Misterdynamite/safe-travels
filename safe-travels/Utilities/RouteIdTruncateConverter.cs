using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace safe_travels.Utilities
{
    public class RouteIdTruncateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var routeId = value as string;
            if (string.IsNullOrEmpty(routeId))
                return string.Empty;
            int idx = routeId.IndexOf('-');
            return idx > 0 ? routeId.Substring(0, idx) : routeId;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}