using API_Tester.AucklandTransportAPI;
using safe_travels.API.AucklandTransportAPI;
using safe_travels.Utilities;
using safe_travels.Models;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace safe_travels.Views;

/// <summary>
/// Displays details for a specific bus stop, including upcoming trips and favorite management.
/// </summary>
public partial class BusDetailPage : ContentPage
{
    private string _stopName;
    private string _stopId;
    private List<TripStopData>? trips;
    private List<TripStopData> _currentTrips = new(); // Store loaded trips for filtering and display
    private System.Threading.Timer? _refreshTimer;
    private bool _isRefreshing = false;
    private bool _isNavigating = false; // Debounce flag

    /// <summary>
    /// Initializes a new instance of the <see cref="BusDetailPage"/> class.
    /// </summary>
    /// <param name="trips">A list of trip stop responses containing trip data for the stop.</param>
    /// <param name="stopName">The name of the bus stop.</param>
    /// <param name="stopId">The unique identifier of the bus stop.</param>
    public BusDetailPage(List<TripStopResponse> trips, string stopName, string stopId)
    {
        InitializeComponent();

        _stopName = stopName;
        _stopId = stopId;
        
        // Initialize with empty list for immediate UI display
        BindingContext = new
        {
            StopName = stopName,
            StopId = stopId,
            Trips = new List<TripStopData>(),
        };
        
        // Process trips asynchronously
        _ = ProcessTripsAsync(trips);
        
        // Start auto-refresh timer (30 seconds)
        StartAutoRefresh();
    }

    /// <summary>
    /// Processes trips asynchronously to avoid blocking the UI.
    /// </summary>
    private async Task ProcessTripsAsync(List<TripStopResponse> trips)
    {
        await Task.Run(() =>
        {
            var tripsList = trips.SelectMany(t => t.data).ToList();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentTrips = tripsList;
                BusCollection.ItemsSource = tripsList;
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            });
        });
    }

    /*
     * Constructor overload to accept List<TripStopData> directly.
     */
    public BusDetailPage(List<TripStopData> trips, string stopName, string stopId)
    {
        InitializeComponent();
        
        _stopName = stopName;
        _stopId = stopId;
        
        // Initialize with empty list for immediate UI display
        BindingContext = new
        {
            StopName = stopName,
            StopId = stopId,
            Trips = new List<TripStopData>(),
        };
        
        // Process trips asynchronously
        _ = ProcessTripsDirectAsync(trips);
        
        // Start auto-refresh timer (30 seconds)
        StartAutoRefresh();
    }

    /// <summary>
    /// Processes trips list asynchronously to avoid blocking the UI.
    /// </summary>
    private async Task ProcessTripsDirectAsync(List<TripStopData> trips)
    {
        await Task.Run(() =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentTrips = trips;
                BusCollection.ItemsSource = trips;
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            });
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Update SaveStopButton text depending on favorite status
        try
        {
            if (!string.IsNullOrWhiteSpace(_stopId) && FavoriteBusManager.IsFavorite(_stopId))
            {
                SaveStopButton.Text = "Remove Stop";
            }
            else
            {
                SaveStopButton.Text = "Save Stop";
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Handles the event when the user clicks to save the stop as a favorite.
    /// If the stop is already saved, offers removal.
    /// Otherwise prompts for an optional nickname and saves the favorite.
    /// </summary>
    private async void OnSaveStopClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_stopId))
        {
            await DisplayAlert("Error", "Stop ID is missing.", "OK");
            return;
        }

        // If already favorite, remove it immediately
        if (FavoriteBusManager.IsFavorite(_stopId))
        {
            FavoriteBusManager.RemoveFavoriteStop(_stopId);
            SaveStopButton.Text = "Save Stop";
            await DisplayAlert("Removed", $"Removed '{_stopName}' from favorites.", "OK");
            return;
        }

        // Not a favorite yet - prompt for nickname then save
        string? nickname = await DisplayPromptAsync("Save Favorite", "Enter a nickname for this stop (optional)", "Save", "Cancel", "", -1, Keyboard.Text, "");

        var fav = new FavoriteStop
        {
            stopId = _stopId,
            stopName = _stopName,
            id = _stopId,
            type = "stop",
            Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname
        };

        FavoriteBusManager.AddFavoriteStop(fav);
        SaveStopButton.Text = "Remove Stop";
        await DisplayAlert("Saved", $"Stop '{fav.DisplayLabel}' saved to favorites.", "OK");
    }

    /// <summary>
    /// Filters the displayed buses by the provided headsign string.
    /// </summary>
    /// <param name="stopHeadsign">The headsign to filter by. If null or empty, all buses are shown.</param>
    public void FilterBuses(string stopHeadsign)
    {
        var trips = _currentTrips;
        if (trips == null)
            return;
        // filter by headsign using a case-insensitive partial match
        var filtered = string.IsNullOrEmpty(stopHeadsign)
            ? trips
            : trips.Where(bus =>
                bus.attributes != null &&
                !string.IsNullOrEmpty(bus.attributes.stopHeadSign) &&
                bus.attributes.stopHeadSign.Contains(stopHeadsign, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        BusCollection.ItemsSource = filtered;
    }

    /// <summary>
    /// Handles the event when the search text changes, updating the bus filter.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The text changed event arguments.</param>
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string input = e.NewTextValue?.Trim() ?? string.Empty;
        FilterBuses(input);
    }

    /// <summary>
    /// Schedules a notification to alert the user 5 minutes before the specified departure time.
    /// </summary>
    /// <param name="departureTime">The departure time as a string.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task departureNotification(string departureTime)
    {
        //parse dateTime
        if (!DateTime.TryParse(departureTime, out DateTime depTime))
        {
            await DisplayAlert("Error", "Invalid departure time format.", "OK");
            return;
        }
        //alerts user 5 minutes before departure
        var alert = TimeSpan.FromMinutes(5);
        var notify = depTime - alert;
        var delay = notify - DateTime.Now;

        if (delay.TotalMilliseconds <= 0)
        {
            return;
        }
        await Task.Delay(delay);
        await DisplayAlert("Reminder", $"Your bus departs at {departureTime} in 10 minutes.", "OK");
    }

    /// <summary>
    /// Handles selection of a bus in the list, shows options to view route or set notification.
    /// </summary>
    private async void OnBusSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            if (e.CurrentSelection?.FirstOrDefault() is TripStopData selectedBus)
            {
                string action = await DisplayActionSheet(
                    $"Bus to '{selectedBus.attributes.stopHeadSign}'",
                    "Cancel",
                    null,
                    "View Full Route",
                    "Set Arrival Alert");

                if (action == "View Full Route")
                {
                    // Navigate to TripStopsPage to show all stops on this trip
                    await Navigation.PushAsync(new TripStopsPage(
                        selectedBus.attributes.tripId,
                        selectedBus.attributes.routeId,
                        selectedBus.attributes.stopHeadSign,
                        selectedBus.attributes.serviceDate,
                        selectedBus.attributes.tripStartTime));
                }
                else if (action == "Set Arrival Alert")
                {
                    string[] options = { "1 minute", "3 minutes", "5 minutes", "10 minutes" };
                    string chosen = await DisplayActionSheet(
                        $"Set alert for bus to '{selectedBus.attributes.stopHeadSign}'?",
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
                        await ScheduleBusArrivalNotification(selectedBus, minutes);
                    }
                }
            }
            BusCollection.SelectedItem = null;
        }
        finally
        {
            await Task.Delay(1000);
            _isNavigating = false;
        }
    }

    /// <summary>
    /// Schedules a notification for the selected bus arrival time minus the chosen minutes.
    /// </summary>
    private async Task ScheduleBusArrivalNotification(TripStopData bus, int minutesBefore)
    {
        string arrivalTime = bus.attributes.arrivalTime;
        if (!DateTime.TryParse(arrivalTime, out DateTime arrTime))
        {
            await DisplayAlert("Error", "Invalid arrival time format.", "OK");
            return;
        }
        var notifyTime = arrTime - TimeSpan.FromMinutes(minutesBefore);
        var delay = notifyTime - DateTime.Now;
        if (delay.TotalMilliseconds <= 0)
        {
            await DisplayAlert("Too Late", $"Bus to '{bus.attributes.stopHeadSign}' arrives in less than {minutesBefore} minutes.", "OK");
            return;
        }
        await DisplayAlert("Alert Set", $"You will be notified {minutesBefore} minutes before arrival.", "OK");
        await Task.Delay(delay);
        await DisplayAlert("Bus Arriving Soon", $"Bus to '{bus.attributes.stopHeadSign}' arrives in {minutesBefore} minutes.", "OK");
    }

    /// <summary>
    /// Starts the auto-refresh timer to update trip data every 30 seconds.
    /// </summary>
    private void StartAutoRefresh()
    {
        // Refresh every 30 seconds (30000 milliseconds)
        _refreshTimer = new System.Threading.Timer(async _ => await RefreshTrips(), null, 30000, 30000);
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
    /// Refreshes the trip data by calling the API again.
    /// </summary>
    private async Task RefreshTrips()
    {
        // Prevent multiple simultaneous refreshes
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            System.Diagnostics.Debug.WriteLine($"Auto-refreshing trips for stop {_stopId}...");
            var stopTripCalls = new InboundTripsAPI();
            var busDetails = await stopTripCalls.GetTripsByStopID(_stopId);
            var tripsList = busDetails.SelectMany(t => t.data).ToList();
            // Update on UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _currentTrips = tripsList;
                BusCollection.ItemsSource = tripsList;
                System.Diagnostics.Debug.WriteLine($"Refreshed {tripsList.Count} trips");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing trips: {ex.Message}");
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
        System.Diagnostics.Debug.WriteLine("Stopped auto-refresh timer");
    }
}