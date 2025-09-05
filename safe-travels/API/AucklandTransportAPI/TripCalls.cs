using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace safe_travels.API.AucklandTransportAPI
{
    class TripCalls
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
        /// <param name="stopIdInput">The partial or full name of the stop to search for.</param>
        /// <returns>A list of <see cref="StopData"/> objects matching the search criteria.</returns>
        public async Task<List<TripData>> GetTripbyStopID(string stopIdInput)
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
                var matchingStops = new List<TripData>();

                // Check if the root JSON object contains a "data" property
                if (root.TryGetProperty("data", out var dataArray))
                {
                    // Iterate through each stop object in the "data" array
                    foreach (var stop in dataArray.EnumerateArray())
                    {
                        var attrs = stop.GetProperty("attributes"); // Get the "attributes" object
                        var stopId = attrs.GetProperty("stop_id").GetString(); // Get the stop name

                        // Check if stop name is not null/empty and contains the input string (case-insensitive)
                        if (!string.IsNullOrEmpty(stopId) &&
                            stopId.IndexOf(stopIdInput, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Add matching stop to the result list
                            matchingStops.Add(new TripData
                            {
                                id = stop.GetProperty("id").GetString() ?? string.Empty,
                                type = stop.GetProperty("type").GetString() ?? string.Empty,
                                attributes = new TripAttributes
                                {
                                    tripId = attrs.GetProperty("trip_id").GetString() ?? string.Empty,
                                    tripHeadsign = attrs.GetProperty("trip_headsign").GetString() ?? string.Empty,
                                    tripStartTime = attrs.GetProperty("trip_start_time").GetString() ?? string.Empty,
                                    routeId = attrs.GetProperty("route_id").GetString() ?? string.Empty,
                                    serviceDate = attrs.GetProperty("service_date").GetString() ?? string.Empty,
                                    stopHeadsign = attrs.GetProperty("stop_headsign").GetString() ?? string.Empty,
                                    directionId = attrs.GetProperty("direction_id").GetInt32(),
                                    shapeId = attrs.GetProperty("shape_id").GetString() ?? string.Empty

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
                return new List<TripData>();
            }
        }

    }

  
    public class  TripData
    {
        public string type { get; set; }
        public string id { get; set; }
        public TripAttributes attributes { get; set; }

    }
    public class  TripAttributes 
    {
        public string tripId { get; set; }
        public string tripHeadsign { get; set; }
        public string tripStartTime { get; set; }
        public string routeId { get; set; }
        public string serviceDate { get; set; }
        public string stopHeadsign { get; set; }
        public int directionId { get; set; }
        public string shapeId { get; set; }

    }
}
