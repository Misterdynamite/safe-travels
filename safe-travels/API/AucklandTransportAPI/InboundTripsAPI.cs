using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using safe_travels.Utilities;
using safe_travels.Models;

namespace safe_travels.API.AucklandTransportAPI
{
    /// <summary>
    /// Provides methods to interact with the Auckland Transport API for retrieving trip information by stop ID.
    /// </summary>
    class InboundTripsAPI
    {
        /// <summary>
        /// The base URL template for the Auckland Transport stop trips API.
        /// </summary>
        private static readonly string apiURL = "https://rest.kennedys.nz/api/stops/{id}/trips";

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
                var requestUrl = $"https://rest.kennedys.nz/api/stops/{stopIdInput}/trips?limit=20";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var matchingTrips = new List<TripStopResponse>();

                if (root.TryGetProperty("trips", out var tripsArray))
                {
                    foreach (var trip in tripsArray.EnumerateArray())
                    {
                        matchingTrips.Add(new TripStopResponse
                        {
                            data = new List<TripStopData>
                            {
                                new TripStopData
                                {
                                    id = trip.GetProperty("trip_id").GetString() ?? string.Empty,
                                    type = "trip",
                                    attributes = new TripStopAttributes
                                    {
                                        arrivalTime = trip.TryGetProperty("arrival_time", out var arrTime) ? arrTime.GetString() ?? string.Empty : string.Empty,
                                        departureTime = trip.TryGetProperty("departure_time", out var depTime2) ? depTime2.GetString() ?? string.Empty : string.Empty,
                                        directionId = trip.TryGetProperty("direction_id", out var dirId) ? dirId.GetInt32() : 0,
                                        dropOffType = 0, 
                                        pickupType = 0, 
                                        routeId = trip.GetProperty("route_id").GetString() ?? string.Empty,
                                        serviceDate = trip.TryGetProperty("service_date", out var servDate) ? servDate.GetString() ?? string.Empty : string.Empty,
                                        shapeId = trip.TryGetProperty("shape_id", out var shId) ? shId.GetString() ?? string.Empty : string.Empty,
                                        stopHeadSign = trip.TryGetProperty("trip_headsign", out var headSign) ? headSign.GetString() ?? string.Empty : string.Empty,
                                        stopId = stopIdInput,
                                        stopSequence = trip.TryGetProperty("stop_sequence", out var seq) ? seq.GetInt32() : 0,
                                        tripId = trip.GetProperty("trip_id").GetString() ?? string.Empty,
                                        tripStartTime = trip.TryGetProperty("trip_start_time", out var startTime) ? startTime.GetString() ?? string.Empty : string.Empty
                                    }
                                }
                            }
                        });
                    }
                }

                Debug.WriteLine($"Fetched {matchingTrips.Count} trips for stop ID {stopIdInput}");
                return matchingTrips;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching trips for stop ID {stopIdInput}: {ex.Message}");
                return new List<TripStopResponse>();
            }
        }
    }
}