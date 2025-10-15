using safe_travels.API.LocationIQ;
using safe_travels.API.AucklandTransportAPI;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using safe_travels.API.AucklandTransportAPI;

namespace safe_travels.Views;

public partial class JourneyPlannerPage : ContentPage
{
    #region Fields & Initialization

    private readonly LocationIQAPI _locationIQ = new();

    private LocationIQGeocodeResult? _originLocation;
    private LocationIQGeocodeResult? _destinationLocation;

    private readonly ObservableCollection<string> _journeyResults = new();
    private readonly ObservableCollection<LocationIQSuggestion> _originSuggestions = new();
    private readonly ObservableCollection<LocationIQSuggestion> _destinationSuggestions = new();

    private CancellationTokenSource? _originDebounceCts;
    private CancellationTokenSource? _destinationDebounceCts;

    private readonly Dictionary<string, LocationIQGeocodeResult?> _originGeocodeCache = new();
    private readonly Dictionary<string, LocationIQGeocodeResult?> _destinationGeocodeCache = new();

    public JourneyPlannerPage()
    {
        InitializeComponent();

        JourneyResults.ItemsSource = _journeyResults;
        OriginSuggestionsView.ItemsSource = _originSuggestions;
        DestinationSuggestionsView.ItemsSource = _destinationSuggestions;
    }

    #endregion

    #region Autocomplete Handlers

    private async void OnOriginTextChanged(object sender, TextChangedEventArgs e)
    {
        _originDebounceCts?.Cancel(); // cancel previous debounce
        _originDebounceCts = new CancellationTokenSource();

        await HandleAutocompleteAsync(
            sender as Entry,
            _originSuggestions,
            OriginSuggestionsView,
            _originDebounceCts.Token,
            isOrigin: true);
    }

    private async void OnDestinationTextChanged(object sender, TextChangedEventArgs e)
    {
        _destinationDebounceCts?.Cancel(); // cancel previous debounce
        _destinationDebounceCts = new CancellationTokenSource();

        await HandleAutocompleteAsync(
            sender as Entry,
            _destinationSuggestions,
            DestinationSuggestionsView,
            _destinationDebounceCts.Token,
            isOrigin: false);
    }

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1000);
    private DateTime _lastRequestTime = DateTime.MinValue;

    private async Task HandleAutocompleteAsync(
        Entry? inputBox,
        ObservableCollection<LocationIQSuggestion> suggestionsCollection,
        CollectionView suggestionsView,
        CancellationToken token,
        bool isOrigin)
    {
        if (inputBox == null)
            return;

        string query = inputBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                suggestionsView.IsVisible = false;
                suggestionsCollection.Clear();
            });
            return;
        }

        try
        {
            // Wait for debounce delay
            await Task.Delay(DebounceDelay, token);
            token.ThrowIfCancellationRequested();

            // Throttle additional requests
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            if (elapsed < MinInterval)
                await Task.Delay(MinInterval - elapsed, token);

            _lastRequestTime = DateTime.UtcNow;

            // Call API
            List<LocationIQSuggestion> suggestions = await _locationIQ.GetAddressSuggestionsAsync(query);
            Debug.WriteLine($"{(isOrigin ? "Origin" : "Destination")} suggestions count: {suggestions.Count}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                suggestionsCollection.Clear();
                foreach (var s in suggestions)
                    suggestionsCollection.Add(s);

                suggestionsView.ItemsSource = suggestionsCollection;
                suggestionsView.IsVisible = suggestionsCollection.Any();
            });
        }
        catch (TaskCanceledException)
        {
            // user kept typing - ignore
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{(isOrigin ? "Origin" : "Destination")} autocomplete error: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current?.MainPage?.DisplayAlert("Error", $"Failed to fetch suggestions: {ex.Message}", "OK"));
        }
    }

    #endregion



    #region Suggestion Selection

    private async void OnOriginSuggestionChosen(object sender, EventArgs e)
        => await HandleSuggestionChosenAsync(sender, true);

    private async void OnDestinationSuggestionChosen(object sender, EventArgs e)
        => await HandleSuggestionChosenAsync(sender, false);

    private async Task HandleSuggestionChosenAsync(object sender, bool isOrigin)
    {
        var selectedText = (sender as Label)?.Text;
        if (string.IsNullOrWhiteSpace(selectedText)) return;

        var cache = isOrigin ? _originGeocodeCache : _destinationGeocodeCache;
        var cached = cache.TryGetValue(selectedText, out var result) ? result : null;

        try
        {
            var location = cached ?? await _locationIQ.GeocodeAddressAsync(selectedText);
            cache[selectedText] = location;

            if (isOrigin)
            {
                _originLocation = location;
                OriginInput.Text = selectedText;
                OriginSuggestionsView.IsVisible = false;
            }
            else
            {
                _destinationLocation = location;
                DestinationInput.Text = selectedText;
                DestinationSuggestionsView.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to geocode {(isOrigin ? "origin" : "destination")}: {ex.Message}", "OK");
        }
    }

    #endregion

    #region Journey Planning

    private async void OnPlanJourneyClicked(object sender, EventArgs e)
    {
        if (_originLocation == null || _destinationLocation == null)
        {
            await DisplayAlert("Error", "Please select both origin and destination.", "OK");
            return;
        }

        var originStops = await FetchStopsAsync(_originLocation);
        var destinationStops = await FetchStopsAsync(_destinationLocation);

        if (!originStops.Any() || !destinationStops.Any())
        {
            await DisplayAlert("Error", "No bus stops found near origin or destination.", "OK");
            return;
        }

        var possibleRoutes = await FindRoutesAsync(originStops, destinationStops);

        _journeyResults.Clear();
        foreach (var route in possibleRoutes)
            _journeyResults.Add(route);

        JourneyResults.IsVisible = true;
    }

    private static async Task<IEnumerable<Stop>> FetchStopsAsync(LocationIQGeocodeResult location)
    {
        return await AucklandTransportAPIClient.FetchStopsNearUser(
            double.Parse(location.lat), double.Parse(location.lon), 500);
    }

    private static async Task<HashSet<string>> FindRoutesAsync(IEnumerable<Stop> originStops, IEnumerable<Stop> destStops)
    {
        var routes = new HashSet<string>();
        var stopTripsCache = new Dictionary<string, List<TripStopResponse>>();

        async Task<List<TripStopResponse>> GetTripsCached(string stopId)
        {
            if (!stopTripsCache.TryGetValue(stopId, out var trips))
            {
                trips = await AucklandTransportAPIClient.GetTripsForStop(stopId);
                stopTripsCache[stopId] = trips;
            }
            return trips;
        }

        // Fetch trips for all nearby stops concurrently
        var allStops = originStops.Concat(destStops).DistinctBy(s => s.stopId).ToList();
        await Task.WhenAll(allStops.Select(s => GetTripsCached(s.stopId)));

        // 1️⃣ Collect all routes serving nearby origin stops
        var originRoutes = new HashSet<string>(
            originStops.SelectMany(o => stopTripsCache[o.stopId])
                       .SelectMany(resp => resp.data.Select(t => t.attributes.routeId)));

        // 2️⃣ Collect all routes serving nearby destination stops
        var destRoutes = new HashSet<string>(
            destStops.SelectMany(d => stopTripsCache[d.stopId])
                     .SelectMany(resp => resp.data.Select(t => t.attributes.routeId)));

        // 3️⃣ Find routes in common (direct connections)
        var directRoutes = originRoutes.Intersect(destRoutes).ToList();

        if (directRoutes.Any())
        {
            foreach (var route in directRoutes)
            {
                // Find the origin stop(s) serving this route
                var originServingStops = originStops
                    .Where(o => stopTripsCache[o.stopId]
                        .Any(resp => resp.data.Any(t => t.attributes.routeId == route)))
                    .Select(o => o.stopName)
                    .Distinct()
                    .ToList();

                foreach (var stopName in originServingStops)
                {
                    routes.Add($"Direct bus route {route} connects origin (from stop \"{stopName}\") and destination (within 1 km).");
                }
            }
        }
        else
        {
            routes.Add("No direct bus route found within 1 km of both locations.");
        }

        return routes;
    }

    #endregion
}
