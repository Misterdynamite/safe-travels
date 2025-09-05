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
        private static readonly string apiURL = "https://api.at.govt.nz/gtfs/v3/stops";

        /// <summary>
        /// The subscription key required for authenticating API requests.
        /// </summary>
        private static readonly string subscriptionKey = "25c926c6234a49c98d52d90a8bd7ac7e";

        /// <summary>
        /// Retrieves a list of stops whose names contain the specified input string.
        /// </summary>
        /// <param name="stopNameInput">The partial or full name of the stop to search for.</param>
        /// <returns>A list of <see cref="StopData"/> objects matching the search criteria.</returns>
        public async Task<List<StopData>> GetStopsByName(string stopNameInput)
        {
            try
            {
                // Log that the API call is starting
                System.Diagnostics.Debug.WriteLine("Starting API call");

                // Create a new HttpClient instance to make the HTTP request
                using var client = new HttpClient();

                // Add headers to the request
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache"); // Prevent cached responses
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey); // API subscription key

                // Send a GET request to the API
                var response = await client.GetAsync(apiURL);

                // Throw an exception if the response status code is not successful (200-299)
                response.EnsureSuccessStatusCode();

                // Read the response content as a string
                var content = await response.Content.ReadAsStringAsync();

                // Parse the JSON response into a JsonDocument
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement; // Get the root element of the JSON

                // List to hold stops that match the input name
                var matchingStops = new List<StopData>();

                // Check if the root JSON object contains a "data" property
                if (root.TryGetProperty("data", out var dataArray))
                {
                    // Iterate through each stop object in the "data" array
                    foreach (var stop in dataArray.EnumerateArray())
                    {
                        var attrs = stop.GetProperty("attributes"); // Get the "attributes" object
                        var stopName = attrs.GetProperty("stop_name").GetString(); // Get the stop name

                        // Check if stop name is not null/empty and contains the input string (case-insensitive)
                        if (!string.IsNullOrEmpty(stopName) &&
                            stopName.IndexOf(stopNameInput, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Add matching stop to the result list
                            matchingStops.Add(new StopData
                            {
                                id = stop.GetProperty("id").GetString() ?? string.Empty,
                                type = stop.GetProperty("type").GetString() ?? string.Empty,
                                attributes = new StopAttributes
                                {
                                    stopId = attrs.GetProperty("stop_id").GetString() ?? string.Empty,
                                    stopName = stopName,
                                    stopLat = attrs.GetProperty("stop_lat").GetDouble(),
                                    stopLong = attrs.GetProperty("stop_lon").GetDouble()
                                }
                            });
                        }
                    }
                }

                // Log that the API call has finished
                System.Diagnostics.Debug.WriteLine("API call finished");

                // Return the list of matching stops
                return matchingStops;
            }
            catch (Exception ex)
            {
                // Log any errors that occur during the API call or parsing
                Console.WriteLine($"An error occurred: {ex.Message}");

                // Return an empty list if an error occurs
                return new List<StopData>();
            }
        }

    }

    /// <summary>
    /// Represents a stop returned by the Auckland Transport API.
    /// </summary>
    public class StopData
    {
        /// <summary>
        /// Gets or sets the type of the stop.
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the stop.
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Gets or sets the attributes of the stop.
        /// </summary>
        public StopAttributes attributes { get; set; }
    }

    /// <summary>
    /// Represents the attributes of a stop.
    /// </summary>
    public class StopAttributes
    {
        /// <summary>
        /// Gets or sets the stop ID.
        /// </summary>
        public string stopId { get; set; }

        /// <summary>
        /// Gets or sets the name of the stop.
        /// </summary>
        public string stopName { get; set; }

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