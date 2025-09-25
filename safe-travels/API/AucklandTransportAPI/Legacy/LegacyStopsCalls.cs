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
    /// Legacy implementation for the original Auckland Transport API for retrieving stop information.
    /// </summary>
    class LegacyStopsCalls
    {
        /// <summary>
        /// The base URL for the Auckland Transport stops API.
        /// </summary>
        private static readonly string apiURL = "https://api.at.govt.nz/gtfs/v3/stops";

        /// <summary>
        /// The subscription key required for authenticating API requests.
        /// </summary>
        private static readonly string subscriptionKey = "25c926c6234a49c98d52d90a8bd7ac7e";

        /// <summary>
        /// Retrieves a list of stops whose names contain the specified input string.
        /// </summary>
        /// <param name="stopNameInput">The partial or full name of the stop to search for.</param>
        /// <returns>A list of <see cref="Stop"/> objects matching the search criteria.</returns>
        public async Task<List<Stop>> GetStopsByName(string stopNameInput)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting Legacy API call");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var response = await client.GetAsync(apiURL);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var matchingStops = new List<Stop>();

                if (root.TryGetProperty("data", out var dataArray))
                {
                    foreach (var stop in dataArray.EnumerateArray())
                    {
                        var attrs = stop.GetProperty("attributes");
                        var stopName = attrs.GetProperty("stop_name").GetString();

                        if (!string.IsNullOrEmpty(stopName) &&
                            stopName.IndexOf(stopNameInput, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchingStops.Add(new Stop
                            {
                                id = stop.GetProperty("id").GetString() ?? string.Empty,
                                type = stop.GetProperty("type").GetString() ?? string.Empty,
                                stopId = attrs.GetProperty("stop_id").GetString() ?? string.Empty,
                                stopName = stopName,
                                stopLat = attrs.GetProperty("stop_lat").GetDouble(),
                                stopLong = attrs.GetProperty("stop_lon").GetDouble()
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("Legacy API call finished");
                return matchingStops;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in Legacy API: {ex.Message}");
                return new List<Stop>();
            }
        }

        /// <summary>
        /// Retrieves a list of stops within a specified distance (in meters) from the given latitude and longitude.
        /// </summary>
        /// <param name="latitude">The latitude to search from.</param>
        /// <param name="longitude">The longitude to search from.</param>
        /// <param name="distanceMeters">The maximum distance in meters from the given location.</param>
        /// <returns>A list of <see cref="Stop"/> objects within the specified proximity.</returns>
        public async Task<List<Stop>> GetStopsByProximity(double latitude, double longitude, double distanceMeters)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting Legacy API call for proximity search");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var response = await client.GetAsync(apiURL);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var matchingStops = new List<Stop>();

                if (root.TryGetProperty("data", out var dataArray))
                {
                    foreach (var stop in dataArray.EnumerateArray())
                    {
                        var attrs = stop.GetProperty("attributes");
                        var stopLat = attrs.GetProperty("stop_lat").GetDouble();
                        var stopLon = attrs.GetProperty("stop_lon").GetDouble();

                        // Calculate distance using Haversine formula
                        double distance = GetDistanceInMeters(latitude, longitude, stopLat, stopLon);

                        if (distance <= distanceMeters)
                        {
                            matchingStops.Add(new Stop
                            {
                                id = stop.GetProperty("id").GetString() ?? string.Empty,
                                type = stop.GetProperty("type").GetString() ?? string.Empty,
                                stopId = attrs.GetProperty("stop_id").GetString() ?? string.Empty,
                                stopName = attrs.GetProperty("stop_name").GetString() ?? string.Empty,
                                stopLat = stopLat,
                                stopLong = stopLon
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("Legacy API call for proximity search finished");
                return matchingStops;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in Legacy API: {ex.Message}");
                return new List<Stop>();
            }
        }

        /// <summary>
        /// Calculates the distance in meters between two latitude/longitude points using the Haversine formula.
        /// </summary>
        private static double GetDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth's radius in meters
            double latRad1 = Math.PI * lat1 / 180.0;
            double latRad2 = Math.PI * lat2 / 180.0;
            double deltaLat = Math.PI * (lat2 - lat1) / 180.0;
            double deltaLon = Math.PI * (lon2 - lon1) / 180.0;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(latRad1) * Math.Cos(latRad2) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
