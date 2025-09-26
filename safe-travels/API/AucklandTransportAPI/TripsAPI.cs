using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace safe_travels.API.AucklandTransportAPI
{
    class TripsAPI
    {
        /// <summary>
        /// The base URL for the Auckland Transport stops API.
        /// </summary>
        private static readonly string apiURL = $"https://rest.kennedys.nz/api/trips";

        /// <summary>
        /// Retrieves a list of stops whose names contain the specified input string.
        /// </summary>
        /// <param name="tripIdInput">The partial or full name of the stop to search for.</param>
        /// <returns>A list of <see cref="StopData"/> objects matching the search criteria.</returns>
        public async Task<List<TripData>> GetTripbyTripIDMatch(string tripIdInput)
        {
            try
            {
                // Log that the API call is starting
                System.Diagnostics.Debug.WriteLine("Starting API call");

                // Create a new HttpClient instance to make the HTTP request
                using var client = new HttpClient();

                // Add headers to the request
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache"); // Prevent cached responses

                // Send a GET request to the API with trip_id filter
                var requestUrl = $"{apiURL}?trip_id={Uri.EscapeDataString(tripIdInput)}";
                var response = await client.GetAsync(requestUrl);

                // Throw an exception if the response status code is not successful (200-299)
                response.EnsureSuccessStatusCode();

                // Read the response content as a string
                var content = await response.Content.ReadAsStringAsync();

                // Parse the JSON response into a JsonDocument
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement; // Get the root element of the JSON

                // List to hold trips that match the input
                var trips = new List<TripData>();

                // Check if the root JSON object contains a "trips" property
                if (root.TryGetProperty("trips", out var tripsArray))
                {
                    foreach (var trip in tripsArray.EnumerateArray())
                    {
                        var tripId = trip.GetProperty("trip_id").GetString();

                        if (!string.IsNullOrEmpty(tripId) &&
                            tripId.IndexOf(tripIdInput, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            trips.Add(new TripData
                            {
                                id = trip.GetProperty("trip_id").GetString() ?? string.Empty,
                                type = "trip", 
                                attributes = new TripAttributes
                                {
                                    tripId = tripId,
                                    tripHeadsign = trip.TryGetProperty("trip_headsign", out var headSign) ? headSign.GetString() ?? string.Empty : string.Empty,
                                    tripStartTime = trip.TryGetProperty("trip_start_time", out var startTime) ? startTime.GetString() ?? string.Empty : string.Empty,
                                    routeId = trip.GetProperty("route_id").GetString() ?? string.Empty,
                                    serviceDate = trip.TryGetProperty("service_date", out var servDate) ? servDate.GetString() ?? string.Empty : string.Empty,
                                    stopHeadsign = trip.TryGetProperty("trip_headsign", out var stopHead) ? stopHead.GetString() ?? string.Empty : string.Empty,
                                    directionId = trip.TryGetProperty("direction_id", out var dirId) ? dirId.GetInt32() : 0,
                                    shapeId = trip.TryGetProperty("shape_id", out var shId) ? shId.GetString() ?? string.Empty : string.Empty
                                }
                            });
                        }
                    }
                }

                // Log that the API call has finished
                System.Diagnostics.Debug.WriteLine("API call finished");

                // Return the list of matching trips
                return trips;
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

  
    public class TripData
    {
        public string type { get; set; } = string.Empty;
        public string id { get; set; } = string.Empty;
        public TripAttributes attributes { get; set; } = new TripAttributes();
    }

    public class TripAttributes
    {
        public string tripId { get; set; } = string.Empty;
        public string tripHeadsign { get; set; } = string.Empty;
        public string tripStartTime { get; set; } = string.Empty;
        public string routeId { get; set; } = string.Empty;
        public string serviceDate { get; set; } = string.Empty;
        public string stopHeadsign { get; set; } = string.Empty;
        public int directionId { get; set; }
        public string shapeId { get; set; } = string.Empty;
    }
}
