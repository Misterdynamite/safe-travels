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
    /// Provides methods to interact with the Auckland Transport API for retrieving trip information by stop ID.
    /// </summary>
    class StopTripsCalls
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
                var requestUrl = $"https://rest.kennedys.nz/api/stops/{stopIdInput}/trips";

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
                                        departureTime = trip.TryGetProperty("departure_time", out var depTime) ? depTime.GetString() ?? string.Empty : string.Empty,
                                        directionId = trip.TryGetProperty("direction_id", out var dirId) ? dirId.GetInt32() : 0,
                                        dropOffType = 0, 
                                        pickupType = 0, 
                                        routeId = trip.GetProperty("route_id").GetString() ?? string.Empty,
                                        serviceDate = trip.TryGetProperty("service_date", out var servDate) ? servDate.GetString() ?? string.Empty : DateTime.Now.ToString("yyyy-MM-dd"),
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

    /// <summary>
    /// Represents a response containing trip stop data from the Auckland Transport API.
    /// </summary>
    public class TripStopResponse
    {
        /// <summary>
        /// Gets or sets the list of trip stop data.
        /// </summary>
        public required List<TripStopData> data { get; set; }
    }

    /// <summary>
    /// Represents the data for a specific trip stop.
    /// </summary>
    public class TripStopData
    {
        /// <summary>
        /// Gets or sets the type of the trip stop.
        /// </summary>
        public required string type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the trip stop.
        /// </summary>
        public required string id { get; set; }

        /// <summary>
        /// Gets or sets the attributes associated with the trip stop.
        /// </summary>
        public required TripStopAttributes attributes { get; set; }

        public override string ToString()
        {
            return $"TripStopData: [Type: {type}, Id: {id}, Attributes: {attributes}]";
        }
    }

    /// <summary>
    /// Represents the attributes of a trip stop, including timing and route information.
    /// </summary>
    public class TripStopAttributes
    {
        /// <summary>
        /// Gets or sets the arrival time at the stop.
        /// </summary>
        public required string arrivalTime { get; set; }

        /// <summary>
        /// Gets or sets the departure time from the stop.
        /// </summary>
        public required string departureTime { get; set; }

        /// <summary>
        /// Gets or sets the direction ID for the trip.
        /// </summary>
        public int directionId { get; set; }

        /// <summary>
        /// Gets or sets the drop-off type for the stop.
        /// </summary>
        public int dropOffType { get; set; }

        /// <summary>
        /// Gets or sets the pickup type for the stop.
        /// </summary>
        public int pickupType { get; set; }

        /// <summary>
        /// Gets or sets the route ID for the trip.
        /// </summary>
        public required string routeId { get; set; }

        /// <summary>
        /// Gets or sets the service date for the trip.
        /// </summary>
        public required string serviceDate { get; set; }

        /// <summary>
        /// Gets or sets the shape ID for the trip.
        /// </summary>
        public required string shapeId { get; set; }

        /// <summary>
        /// Gets or sets the head sign for the stop.
        /// </summary>
        public required string stopHeadSign { get; set; }

        /// <summary>
        /// Gets or sets the stop ID.
        /// </summary>
        public required string stopId { get; set; }

        /// <summary>
        /// Gets or sets the sequence number of the stop in the trip.
        /// </summary>
        public int stopSequence { get; set; }

        /// <summary>
        /// Gets or sets the trip ID.
        /// </summary>
        public required string tripId { get; set; }

        /// <summary>
        /// Gets or sets the start time of the trip.
        /// </summary>
        public required string tripStartTime { get; set; }

        public override string ToString()
        {
            return $"[ArrivalTime: {arrivalTime}, DepartureTime: {departureTime}, DirectionId: {directionId}, DropOffType: {dropOffType}, PickupType: {pickupType}, RouteId: {routeId}, ServiceDate: {serviceDate}, ShapeId: {shapeId}, StopHeadSign: {stopHeadSign}, StopId: {stopId}, StopSequence: {stopSequence}, TripId: {tripId}, TripStartTime: {tripStartTime}]";
        }
    }
}




