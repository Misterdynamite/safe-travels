using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace safe_travels.API.AucklandTransportAPI.Legacy
{
    /// <summary>
    /// Legacy implementation for the original Auckland Transport API for retrieving trip information by stop ID.
    /// </summary>
    class LegacyStopTripsCalls
    {
        /// <summary>
        /// The base URL template for the Auckland Transport stop trips API.
        /// </summary>
        private static readonly string apiURL = "https://api.at.govt.nz/gtfs/v3/stops/{id}/stoptrips?filter[date]={filter[date]}&filter[start_hour]={filter[start_hour]}[&filter[hour_range]]";

        /// <summary>
        /// The subscription key required for authenticating requests to the Auckland Transport API.
        /// </summary>
        private static readonly string subscriptionKey = "25c926c6234a49c98d52d90a8bd7ac7e";

        /// <summary>
        /// Retrieves a list of trips for a specified stop ID from the Auckland Transport API.
        /// </summary>
        /// <param name="stopIdInput">The stop ID to query trips for.</param>
        /// <returns>
        /// A list of <see cref="TripStopResponse"/> objects containing trip data for the specified stop ID.
        /// Returns an empty list if no trips are found or an error occurs.
        /// </returns>
        public async Task<List<TripStopResponse>> GetTripsByStopID(string stopIdInput)
        {
            try
            {
                var baseUrl = $"https://api.at.govt.nz/gtfs/v3/stops/{stopIdInput}/stoptrips";
                var queryParams = new Dictionary<string, string>
                {
                    ["filter[date]"] = DateTime.Now.ToString("yyyy-MM-dd"),
                    ["filter[start_hour]"] = DateTime.Now.Hour.ToString(),
                    ["filter[hour_range]"] = "3"
                };

                var uriBuilder = new UriBuilder(baseUrl);
                var query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                uriBuilder.Query = query;

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var response = await client.GetAsync(uriBuilder.Uri);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var matchingTrips = new List<TripStopResponse>();

                if (root.TryGetProperty("data", out var dataArray))
                {
                    foreach (var trip in dataArray.EnumerateArray())
                    {
                        var attrs = trip.GetProperty("attributes");
                        var stopId = attrs.GetProperty("stop_id").GetString();

                        if (stopId == null)
                        {
                            Console.WriteLine("Stop ID is null.");
                        }
                        else if (!stopId.Equals(stopIdInput, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"Skipping trip with stop ID {stopId} as it does not match input {stopIdInput}.");
                            continue;
                        }

                        matchingTrips.Add(new TripStopResponse
                        {
                            data = new List<TripStopData>
                            {
                                new TripStopData
                                {
                                    id = trip.GetProperty("id").GetString() ?? string.Empty,
                                    type = trip.GetProperty("type").GetString() ?? string.Empty,
                                    attributes = new TripStopAttributes
                                    {
                                        arrivalTime = attrs.GetProperty("arrival_time").GetString() ?? string.Empty,
                                        departureTime = attrs.GetProperty("departure_time").GetString() ?? string.Empty,
                                        directionId = attrs.GetProperty("direction_id").GetInt32(),
                                        dropOffType = attrs.GetProperty("drop_off_type").GetInt32(),
                                        pickupType = attrs.GetProperty("pickup_type").GetInt32(),
                                        routeId = attrs.GetProperty("route_id").GetString() ?? string.Empty,
                                        serviceDate = attrs.GetProperty("service_date").GetString() ?? string.Empty,
                                        shapeId = attrs.GetProperty("shape_id").GetString() ?? string.Empty,
                                        stopHeadSign = attrs.GetProperty("stop_headsign").GetString() ?? string.Empty,
                                        stopId = attrs.GetProperty("stop_id").GetString() ?? string.Empty,
                                        stopSequence = attrs.GetProperty("stop_sequence").GetInt32(),
                                        tripId = attrs.GetProperty("trip_id").GetString() ?? string.Empty,
                                        tripStartTime = attrs.GetProperty("trip_start_time").GetString() ?? string.Empty
                                    }
                                }
                            }
                        });
                    }
                }

                Debug.WriteLine($"Fetched {matchingTrips.Count} trips for stop ID {stopIdInput} using Legacy API");
                return matchingTrips;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching trips for stop ID {stopIdInput} using Legacy API: {ex.Message}");
                return new List<TripStopResponse>();
            }
        }
    }
}
