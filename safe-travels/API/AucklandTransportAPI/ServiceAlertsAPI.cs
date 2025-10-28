using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using safe_travels.Models;

namespace safe_travels.API.AucklandTransportAPI
{
    /// <summary>
    /// Provides methods to interact with the new Auckland Transport API for retrieving service alerts.
    /// </summary>
    class ServiceAlertsAPI
    {
        /// <summary>
        /// The base URL for the Auckland Transport alerts API.
        /// </summary>
        private static readonly string apiURL = "https://rest.kennedys.nz/api";

        /// <summary>
        /// Gets service alerts for a specific stop ID using the new Auckland Transport API.
        /// </summary>
        /// <param name="stopId">The stop ID to get alerts for.</param>
        /// <returns>A list of service alerts affecting the specified stop.</returns>
        public async Task<List<ServiceAlert>> GetAlertsByStopIdAsync(string stopId)
        {
            try
            {
                Debug.WriteLine($"Fetching service alerts for stop: {stopId}");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                var requestUrl = $"{apiURL}/stops/{Uri.EscapeDataString(stopId)}/alerts";
                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                var alerts = ParseAlertsResponse(content);
                Debug.WriteLine($"Fetched {alerts.Count} service alerts for stop {stopId} from new API");
                return alerts;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching service alerts for stop {stopId}: {ex.Message}");
                return new List<ServiceAlert>();
            }
        }

        /// <summary>
        /// Gets service alerts for a specific route ID using the new Auckland Transport API.
        /// </summary>
        /// <param name="routeId">The route ID to get alerts for.</param>
        /// <returns>A list of service alerts affecting the specified route.</returns>
        public async Task<List<ServiceAlert>> GetAlertsByRouteIdAsync(string routeId)
        {
            try
            {
                Debug.WriteLine($"Fetching service alerts for route: {routeId}");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                var requestUrl = $"{apiURL}/routes/{Uri.EscapeDataString(routeId)}/alerts";
                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                var alerts = ParseAlertsResponse(content);
                Debug.WriteLine($"Fetched {alerts.Count} service alerts for route {routeId} from new API");
                return alerts;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching service alerts for route {routeId}: {ex.Message}");
                return new List<ServiceAlert>();
            }
        }

        /// <summary>
        /// Parses the JSON response from the alerts API into a list of ServiceAlert objects.
        /// </summary>
        /// <param name="jsonContent">The JSON response content.</param>
        /// <returns>A list of parsed ServiceAlert objects.</returns>
        private List<ServiceAlert> ParseAlertsResponse(string jsonContent)
        {
            var alerts = new List<ServiceAlert>();

            try
            {
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                // Check if there's an "alerts" array in the response
                if (root.TryGetProperty("alerts", out var alertsArray))
                {
                    foreach (var alertElement in alertsArray.EnumerateArray())
                    {
                        var alert = new ServiceAlert
                        {
                            Id = alertElement.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Header = alertElement.TryGetProperty("header", out var header) ? header.GetString() ?? "" : "",
                            Description = alertElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                            ActivePeriods = new List<AlertPeriod>(),
                            Entities = new List<AlertEntity>()
                        };

                        // Parse active periods
                        if (alertElement.TryGetProperty("active_periods", out var periodsArray))
                        {
                            foreach (var period in periodsArray.EnumerateArray())
                            {
                                alert.ActivePeriods.Add(new AlertPeriod
                                {
                                    Start = period.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.Number
                                        ? start.GetInt64()
                                        : 0,
                                    End = period.TryGetProperty("end", out var end) && end.ValueKind == JsonValueKind.Number
                                        ? end.GetInt64()
                                        : 0
                                });
                            }
                        }

                        // Parse affected entities
                        if (alertElement.TryGetProperty("informed_entities", out var entitiesArray))
                        {
                            foreach (var entity in entitiesArray.EnumerateArray())
                            {
                                alert.Entities.Add(new AlertEntity
                                {
                                    AgencyId = entity.TryGetProperty("agency_id", out var agencyId) ? agencyId.GetString() ?? "" : "",
                                    RouteId = entity.TryGetProperty("route_id", out var routeId) ? routeId.GetString() ?? "" : "",
                                    StopId = entity.TryGetProperty("stop_id", out var stopId) ? stopId.GetString() ?? "" : ""
                                });
                            }
                        }

                        alerts.Add(alert);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing alerts response: {ex.Message}");
            }

            return alerts;
        }

        /// <summary>
        /// Checks if a specific alert is currently active and affects the given route/stop/agency.
        /// </summary>
        /// <param name="alert">The service alert to check.</param>
        /// <param name="now">The current date/time.</param>
        /// <param name="routeId">The route ID to check against (optional).</param>
        /// <param name="stopId">The stop ID to check against (optional).</param>
        /// <param name="agencyId">The agency ID to check against (optional).</param>
        /// <returns>True if the alert is active and affects the specified entities.</returns>
        public bool IsAlertActive(
            ServiceAlert alert,
            DateTime now,
            string routeId = "",
            string stopId = "",
            string agencyId = "")
        {
            foreach (var period in alert.ActivePeriods)
            {
                DateTime start = DateTimeOffset.FromUnixTimeSeconds(period.Start).DateTime;
                DateTime end = DateTimeOffset.FromUnixTimeSeconds(period.End).DateTime;

                if (now >= start && now <= end)
                {
                    // Check if alert affects the route/stop/agency
                    foreach (var entity in alert.Entities)
                    {
                        if ((!string.IsNullOrEmpty(entity.RouteId) && entity.RouteId == routeId) ||
                            (!string.IsNullOrEmpty(entity.StopId) && entity.StopId == stopId) ||
                            (!string.IsNullOrEmpty(entity.AgencyId) && entity.AgencyId == agencyId))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a trip is currently running based on service alerts and schedule.
        /// </summary>
        /// <param name="trip">The trip stop attributes to check.</param>
        /// <param name="alerts">The list of service alerts to check against.</param>
        /// <param name="now">The current date/time.</param>
        /// <param name="agencyId">The agency ID (optional).</param>
        /// <returns>True if the trip is running, false otherwise.</returns>
        public bool IsServiceRunning(TripStopAttributes trip, List<ServiceAlert> alerts, DateTime now, string agencyId = "")
        {
            DateTime serviceDate;
            bool parsed = DateTime.TryParse(trip.serviceDate, out serviceDate) || DateTime.TryParseExact(trip.serviceDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out serviceDate);

            if ( !parsed || serviceDate.Date != now.Date)
                return false;
            //parse start and end times
            if (!TimeSpan.TryParse(trip.tripStartTime, out TimeSpan startTime) ||
                !TimeSpan.TryParse(trip.arrivalTime, out TimeSpan endTime))
            {
                return false;
            }

            DateTime tripStart = serviceDate.Date.Add(startTime);
            DateTime tripEnd = serviceDate.Date.Add(endTime);
            if (now < tripStart || now > tripEnd)
                return false;


            // Check pickup / dropoff
            if (trip.pickupType > 1 || trip.dropOffType > 1)
                return false;
            // Check service alerts
            foreach (var alert in alerts)
            {
                if (IsAlertActive(alert, now, trip.routeId, trip.stopId, agencyId))
                    return false;
            }

            return true;
        }
    }
}