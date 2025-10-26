using safe_travels.API.AucklandTransportAPI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace safe_travels.Views;

/// <summary>
/// Displays all stops for a specific bus trip, including arrival times and countdown timers.
/// </summary>
public partial class TripStopsPage : ContentPage
{
    private string _tripId;
    private string _routeId;
    private string _tripHeadsign;
    private List<TripStopData>? _allStops;
    private System.Threading.Timer? _refreshTimer;
    private bool _isRefreshing = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="TripStopsPage"/> class.
    /// </summary>
    /// <param name="tripId">The unique identifier of the trip.</param>
    /// <param name="routeId">The route ID of the trip.</param>
    /// <param name="tripHeadsign">The destination headsign of the trip.</param>
    /// <param name="serviceDate">The service date for the trip.</param>
    /// <param name="tripStartTime">The start time of the trip.</param>
    public TripStopsPage(string tripId, string routeId, string tripHeadsign, string serviceDate = "", string tripStartTime = "")
    {
        InitializeComponent();

        _tripId = tripId;
        _routeId = routeId;
        _tripHeadsign = tripHeadsign;

        BindingContext = new
        {
            TripId = tripId,
            RouteId = routeId,
            TripHeadsign = tripHeadsign,
            Stops = new List<TripStopData>()
        };

        // Load stops for this trip asynchronously
        _ = LoadTripStopsAsync();

        // Start auto-refresh timer (30 seconds)
        StartAutoRefresh();
    }

    /// <summary>
    /// Loads all stops for the current trip from the API asynchronously.
    /// </summary>
    private async Task LoadTripStopsAsync()
    {
        try
        {
            // Run API call on background thread
            var api = new TripStopsAPI();
            var stops = await Task.Run(async () => await api.GetStopsByTripID(_tripId));

            _allStops = stops;

            System.Diagnostics.Debug.WriteLine($"=== TripStopsPage Loading {stops.Count} stops ===");
            foreach (var stop in stops.Take(5)) // Just first 5 for debugging
            {
                System.Diagnostics.Debug.WriteLine($"\nStop: {stop.attributes.stopHeadSign} (Seq: {stop.attributes.stopSequence})");
                System.Diagnostics.Debug.WriteLine($"  Arrival: '{stop.attributes.arrivalTime}'");
                System.Diagnostics.Debug.WriteLine($"  TripStart: '{stop.attributes.tripStartTime}'");
                System.Diagnostics.Debug.WriteLine($"  ServiceDate: '{stop.attributes.serviceDate}'");
                System.Diagnostics.Debug.WriteLine($"  Countdown: '{stop.attributes.ArrivalCountdown}'");
            }

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BindingContext = new
                {
                    TripId = _tripId,
                    RouteId = _routeId,
                    TripHeadsign = _tripHeadsign,
                    Stops = stops
                };
                
                // Hide loading indicator
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading trip stops: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                await DisplayAlert("Error", "Failed to load stops for this trip.", "OK");
            });
        }
    }

    /// <summary>
    /// Filters the displayed stops by the provided search string.
    /// </summary>
    /// <param name="searchText">The text to filter by. If null or empty, all stops are shown.</param>
    private void FilterStops(string searchText)
    {
        if (_allStops == null)
            return;

        // Filter by stop name using a case-insensitive partial match
        var filtered = string.IsNullOrEmpty(searchText)
            ? _allStops
            : _allStops.Where(stop =>
                stop.attributes != null &&
                !string.IsNullOrEmpty(stop.attributes.stopHeadSign) &&
                stop.attributes.stopHeadSign.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            ).ToList();

        StopsCollection.ItemsSource = filtered;
    }

    /// <summary>
    /// Handles the event when the search text changes, updating the stop filter.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The text changed event arguments.</param>
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string input = e.NewTextValue?.Trim() ?? string.Empty;
        FilterStops(input);
    }

    /// <summary>
    /// Handles selection of a stop in the list.
    /// </summary>
    private async void OnStopSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is TripStopData selectedStop)
        {
            string[] options = { "1 minute", "3 minutes", "5 minutes", "10 minutes" };
            string chosen = await DisplayActionSheet(
                $"Set alert for arrival at '{selectedStop.attributes.stopHeadSign}'?",
                "Cancel", null, options);
            
            int minutes = chosen switch
            {
                "1 minute" => 1,
                "3 minutes" => 3,
                "5 minutes" => 5,
                "10 minutes" => 10,
                _ => 0
            };
            
            if (minutes > 0)
            {
                await ScheduleStopArrivalNotification(selectedStop, minutes);
            }
        }
        StopsCollection.SelectedItem = null;
    }

    /// <summary>
    /// Schedules a notification for the selected stop arrival time minus the chosen minutes.
    /// </summary>
    private async Task ScheduleStopArrivalNotification(TripStopData stop, int minutesBefore)
    {
        string arrivalTime = stop.attributes.arrivalTime;
        if (!DateTime.TryParse(arrivalTime, out DateTime arrTime))
        {
            await DisplayAlert("Error", "Invalid arrival time format.", "OK");
            return;
        }
        
        var notifyTime = arrTime - TimeSpan.FromMinutes(minutesBefore);
        var delay = notifyTime - DateTime.Now;
        
        if (delay.TotalMilliseconds <= 0)
        {
            await DisplayAlert("Too Late", $"Bus arrives at '{stop.attributes.stopHeadSign}' in less than {minutesBefore} minutes.", "OK");
            return;
        }
        
        await DisplayAlert("Alert Set", $"You will be notified {minutesBefore} minutes before arrival at {stop.attributes.stopHeadSign}.", "OK");
        
        await Task.Delay(delay);
        await DisplayAlert("Bus Arriving Soon", $"Bus arrives at '{stop.attributes.stopHeadSign}' in {minutesBefore} minutes.", "OK");
    }

    /// <summary>
    /// Starts the auto-refresh timer to update stop data every 30 seconds.
    /// </summary>
    private void StartAutoRefresh()
    {
        // Refresh every 30 seconds (30000 milliseconds)
        _refreshTimer = new System.Threading.Timer(async _ => await RefreshStops(), null, 30000, 30000);
    }

    /// <summary>
    /// Stops the auto-refresh timer.
    /// </summary>
    private void StopAutoRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    /// <summary>
    /// Refreshes the stop data by calling the API again.
    /// </summary>
    private async Task RefreshStops()
    {
        // Prevent multiple simultaneous refreshes
        if (_isRefreshing) return;
        
        _isRefreshing = true;
        
        try
        {
            var api = new TripStopsAPI();
            var stops = await api.GetStopsByTripID(_tripId);
            
            _allStops = stops;
            
            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var currentSearchText = StopSearchBar.Text?.Trim();
                if (string.IsNullOrEmpty(currentSearchText))
                {
                    // Update binding context with refreshed stops
                    BindingContext = new
                    {
                        TripId = _tripId,
                        RouteId = _routeId,
                        TripHeadsign = _tripHeadsign,
                        Stops = stops
                    };
                }
                else
                {
                    // Re-apply filter
                    FilterStops(currentSearchText);
                }
            });
            
            System.Diagnostics.Debug.WriteLine($"Refreshed {stops.Count} stops for trip {_tripId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing stops: {ex.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// Called when the page disappears. Stops the auto-refresh timer.
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAutoRefresh();
    }
}
