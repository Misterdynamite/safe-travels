using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace safe_travels.API.AucklandTransportAPI
{
    /// <summary>
    /// Provides methods to interact with the Auckland Transport API for retrieving stop information.
    /// </summary>
    class StopsCalls
    {
        /// <summary>
        /// The base URL for the Auckland Transport stops API.
        /// </summary>
        private static readonly string apiURL = "https://rest.kennedys.nz/api/stops";

        /// <summary>
        /// Retrieves a list of stops whose names contain the specified input string.
        /// </summary>
        /// <param name="stopNameInput">The partial or full name of the stop to search for.</param>
        /// <returns>A list of <see cref="Stop"/> objects matching the search criteria.</returns>
        public async Task<List<Stop>> GetStopsByName(string stopNameInput)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting API call");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                // Use the new API with search parameter
                var requestUrl = $"{apiURL}?search={Uri.EscapeDataString(stopNameInput)}";
                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var matchingStops = new List<Stop>();

                if (root.TryGetProperty("stops", out var stopsArray))
                {
                    foreach (var stop in stopsArray.EnumerateArray())
                    {
                        matchingStops.Add(new Stop
                        {
                            id = stop.GetProperty("stop_id").GetString() ?? string.Empty,
                            type = "stop",
                            stopId = stop.GetProperty("stop_id").GetString() ?? string.Empty,
                            stopName = stop.GetProperty("stop_name").GetString() ?? string.Empty,
                            stopLat = stop.GetProperty("stop_lat").GetDouble(),
                            stopLong = stop.GetProperty("stop_lon").GetDouble()
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine("API call finished");
                return matchingStops;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("Starting API call for proximity search");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");


                var response = await client.GetAsync($"{apiURL}?per_page=500");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var matchingStops = new List<Stop>();

                if (root.TryGetProperty("stops", out var stopsArray))
                {
                    foreach (var stop in stopsArray.EnumerateArray())
                    {
                        var stopLat = stop.GetProperty("stop_lat").GetDouble();
                        var stopLon = stop.GetProperty("stop_lon").GetDouble();

                        // Calculate distance using Haversine formula
                        double distance = GetDistanceInMeters(latitude, longitude, stopLat, stopLon);

                        if (distance <= distanceMeters)
                        {
                            matchingStops.Add(new Stop
                            {
                                id = stop.GetProperty("stop_id").GetString() ?? string.Empty,
                                type = "stop",
                                stopId = stop.GetProperty("stop_id").GetString() ?? string.Empty,
                                stopName = stop.GetProperty("stop_name").GetString() ?? string.Empty,
                                stopLat = stopLat,
                                stopLong = stopLon
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("API call for proximity search finished");
                return matchingStops;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
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

    /// <summary>
    /// Represents a stop returned by the Auckland Transport API.
    /// </summary>
    public class Stop
    {
        /// <summary>
        /// Gets or sets the type of the stop.
        /// </summary>
        public required string type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the stop.
        /// </summary>
        public required string id { get; set; }

        /// <summary>
        /// Gets or sets the stop ID.
        /// </summary>
        public required string stopId { get; set; }

        /// <summary>
        /// Gets or sets the name of the stop.
        /// </summary>
        public required string stopName { get; set; }

        /// <summary>
        /// Gets or sets the latitude of the stop.
        /// </summary>
        public double stopLat { get; set; }

        /// <summary>
        /// Gets or sets the longitude of the stop.
        /// </summary>
        public double stopLong { get; set; }
    }
}