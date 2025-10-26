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
    /// Provides methods to interact with the Auckland Transport API for retrieving stop information for a specific trip.
    /// </summary>
    class TripStopsAPI
    {
        /// <summary>
        /// The base URL template for the Auckland Transport trip stops API.
        /// </summary>
        private static readonly string apiURL = "https://rest.kennedys.nz/api/trips/{trip_id}/stops";

        /// <summary>
        /// Retrieves a list of stops for a specified trip ID from the Auckland Transport API.
        /// </summary>
        /// <param name="tripId">The trip ID to query stops for.</param>
        /// <returns>
        /// A list of <see cref="TripStopData"/> objects containing stop data for the specified trip.
        /// Returns an empty list if no stops are found or an error occurs.
        /// </returns>
        public async Task<List<TripStopData>> GetStopsByTripID(string tripId)
        {
            try
            {
                var requestUrl = $"https://rest.kennedys.nz/api/trips/{tripId}/stops";

                Debug.WriteLine($"Fetching stops for trip ID: {tripId}");
                Debug.WriteLine($"Request URL: {requestUrl}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var stops = new List<TripStopData>();

                if (root.TryGetProperty("stops", out var stopsArray))
                {
                    foreach (var stop in stopsArray.EnumerateArray())
                    {
                        stops.Add(new TripStopData
                        {
                            id = stop.GetProperty("stop_id").GetString() ?? string.Empty,
                            type = "stop",
                            attributes = new TripStopAttributes
                            {
                                arrivalTime = stop.TryGetProperty("arrival_time", out var arrTime) ? arrTime.GetString() ?? string.Empty : string.Empty,
                                departureTime = stop.TryGetProperty("departure_time", out var depTime) ? depTime.GetString() ?? string.Empty : string.Empty,
                                directionId = stop.TryGetProperty("direction_id", out var dirId) ? dirId.GetInt32() : 0,
                                dropOffType = stop.TryGetProperty("drop_off_type", out var dropOff) ? dropOff.GetInt32() : 0,
                                pickupType = stop.TryGetProperty("pickup_type", out var pickup) ? pickup.GetInt32() : 0,
                                routeId = stop.TryGetProperty("route_id", out var routeId) ? routeId.GetString() ?? string.Empty : string.Empty,
                                serviceDate = stop.TryGetProperty("service_date", out var servDate) ? servDate.GetString() ?? string.Empty : string.Empty,
                                shapeId = stop.TryGetProperty("shape_id", out var shId) ? shId.GetString() ?? string.Empty : string.Empty,
                                stopHeadSign = stop.TryGetProperty("stop_name", out var stopName) ? stopName.GetString() ?? string.Empty : string.Empty,
                                stopId = stop.GetProperty("stop_id").GetString() ?? string.Empty,
                                stopSequence = stop.TryGetProperty("stop_sequence", out var seq) ? seq.GetInt32() : 0,
                                tripId = tripId,
                                tripStartTime = stop.TryGetProperty("trip_start_time", out var startTime) ? startTime.GetString() ?? string.Empty : string.Empty
                            }
                        });
                    }
                }

                Debug.WriteLine($"Fetched {stops.Count} stops for trip ID {tripId}");
                return stops;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching stops for trip ID {tripId}: {ex.Message}");
                return new List<TripStopData>();
            }
        }
    }
}
