using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using safe_travels.Models;

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
                System.Diagnostics.Debug.WriteLine("Starting API call");
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

                System.Diagnostics.Debug.WriteLine("API call finished");
                return matchingStops;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return new List<Stop>();
            }
        }
    }
}