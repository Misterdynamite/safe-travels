using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace safe_travels.API.AucklandTransportAPI
{
    class StopTripsCalls
    {
        private static readonly string apiURL = "https://api.at.govt.nz/gtfs/v3/stops/{id}/stoptrips?filter[date]={filter[date]}&filter[start_hour]={filter[start_hour]}[&filter[hour_range]]";

        private static readonly string subscriptionKey = "25c926c6234a49c98d52d90a8bd7ac7e";

        public async Task<List<TripStopResponse>> GetTripsByStopID(string stopIdInput)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var url = apiURL
                    .Replace("{id}", stopIdInput)
                    .Replace("{filter[date]}", DateTime.Now.ToString("yyyy-MM-dd"))
                    .Replace("{filter[start_hour]}", DateTime.Now.Hour.ToString());

                var response = await client.GetAsync(url);
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

                Debug.WriteLine($"Fetched {matchingTrips.Count} trips for stop ID {stopIdInput}");
                return matchingTrips;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching trips for stop ID {stopIdInput}: {ex.Message}");
                return new List<TripStopResponse>();
            }
        }

              




public class TripStopResponse
        {
            public List<TripStopData> data { get; set; }
        }

        public class TripStopData
        {
            public string type { get; set; }
            public string id { get; set; }
            public TripStopAttributes attributes { get; set; }
        }
        public class TripStopAttributes
        {

            public string arrivalTime { get; set; }
            public string departureTime { get; set; }
            public int directionId { get; set; }

            public int dropOffType { get; set; }

            public int pickupType { get; set; }

            public string routeId { get; set; }

            public string serviceDate { get; set; }

            public string shapeId { get; set; }

            public string stopHeadSign { get; set; }

            public string stopId { get; set; }

            public int stopSequence { get; set; }

            public string tripId { get; set; }

            public string tripStartTime { get; set; }




        }
    }
}



