namespace safe_travels.Views;

using API_Tester.AucklandTransportAPI;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;


public partial class ServiceAlertsPage : ContentPage
{
    private readonly API_Tester.AucklandTransportAPI.ServiceUpdates _serviceUpdates = new();
    private readonly ObservableCollection<ServiceAlertDisplay> _alerts = new();

    public ServiceAlertsPage()
    {
        InitializeComponent();
        AlertsCollection.ItemsSource = _alerts;
        AccessibleAlertsCollection.ItemsSource = _alerts;
        LoadAlertsAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        bool isAccessibilityMode = Preferences.Get("AccessibilityMode", false);
        NormalView.IsVisible = !isAccessibilityMode;
        AccessibilityView.IsVisible = isAccessibilityMode;
    }

    private async void LoadAlertsAsync()
    {
        try
        {
            // ?? Example: fetch all alerts related to a route (could be parameterized)
            var alerts = await _serviceUpdates.GetServiceAlertsByRouteAsync("NX1");

            _alerts.Clear();

            foreach (var alert in alerts)
            {
                string startStr = "";
                string endStr = "";

                if (alert.ActivePeriods?.Any() == true)
                {
                    var first = alert.ActivePeriods.First();
                    var start = DateTimeOffset.FromUnixTimeSeconds(first.Start).LocalDateTime;
                    var end = DateTimeOffset.FromUnixTimeSeconds(first.End).LocalDateTime;
                    startStr = start.ToString("g");
                    endStr = end.ToString("g");
                }

                string routeText = alert.Entities?.Any(e => !string.IsNullOrEmpty(e.RouteId)) == true
                    ? $"Route: {string.Join(", ", alert.Entities.Where(e => !string.IsNullOrEmpty(e.RouteId)).Select(e => e.RouteId))}"
                    : "General Alert";

                _alerts.Add(new ServiceAlertDisplay
                {
                    HeaderText = !string.IsNullOrWhiteSpace(alert.Header) ? alert.Header : "Service Update",
                    Description = !string.IsNullOrWhiteSpace(alert.Description) ? alert.Description : "No details available.",
                    RouteText = routeText,
                    ActivePeriod = string.IsNullOrEmpty(startStr) ? "Active period not available" : $"{startStr} - {endStr}"
                });
            }

            if (_alerts.Count == 0)
            {
                await DisplayAlert("No Alerts", "There are currently no active service alerts.", "OK");
                SemanticScreenReader.Announce("No service alerts found.");
            }
            else
            {
                SemanticScreenReader.Announce($"Loaded {_alerts.Count} service alerts.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading alerts: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load alerts: {ex.Message}", "OK");
        }
    }

    private class ServiceAlertDisplay
    {
        public string HeaderText { get; set; }
        public string Description { get; set; }
        public string RouteText { get; set; }
        public string ActivePeriod { get; set; }
    }
}