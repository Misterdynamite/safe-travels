using safe_travels.API.AucklandTransportAPI;
using System.Text.Json;

namespace API_Tester.AucklandTransportAPI
{
    class ServiceUpdates
    {
        /// <summary>
        /// The base URL for the Auckland Transport trips API.
        /// </summary>
        private static readonly string apiURL = $"https://api.at.govt.nz/realtime/legacy/servicealerts";


        /// <summary>
        /// The subscription key required for authenticating API requests.
        /// </summary>
        private static readonly string subscriptionKey = "25c926c6234a49c98d52d90a8bd7ac7e";

        /// <summary>
        /// Gets service alerts for a specific stop ID using the Auckland Transport Legacy API.
        /// </summary>
        /// <param name="stopIdInput"></param>
        /// <returns></returns>
        public async Task<List<ServiceAlert>> GetLegacyServiceAlertsAsync(string stopIdInput)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await client.GetAsync(apiURL); // legacy endpoint
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var alerts = new List<ServiceAlert>();

                if (root.TryGetProperty("response", out var responseObj) &&
                    responseObj.TryGetProperty("entity", out var entityArray))
                {

                    foreach (var entity in entityArray.EnumerateArray())
                    {
                        if (!entity.TryGetProperty("alert", out var alertObj)) continue;

                        var headerText = alertObj.GetProperty("header_text")
                            .GetProperty("translation")[0].GetProperty("text").GetString();

                        var descriptionText = alertObj.GetProperty("description_text")
                            .GetProperty("translation")[0].GetProperty("text").GetString();

                        var activePeriods = new List<AlertPeriod>();
                        foreach (var period in alertObj.GetProperty("active_period").EnumerateArray())
                        {
                            activePeriods.Add(new AlertPeriod
                            {
                                Start = period.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.Number
                                    ? start.GetInt64()
                                    : 0,
                                End = period.TryGetProperty("end", out var end) && end.ValueKind == JsonValueKind.Number
                                    ? end.GetInt64()
                                    : 0
                            });
                        }

                        var entities = new List<AlertEntity>();
                        foreach (var informed in alertObj.GetProperty("informed_entity").EnumerateArray())
                        {
                            var stopId = informed.TryGetProperty("stop_id", out var stopIdProp) ? stopIdProp.GetString() ?? "" : "";

                            if (!string.IsNullOrEmpty(stopIdInput) &&
                                !stopId.Equals(stopIdInput, StringComparison.OrdinalIgnoreCase))
                                continue;

                            entities.Add(new AlertEntity
                            {
                                StopId = stopId,
                                RouteId = informed.TryGetProperty("route_id", out var routeId) ? routeId.GetString() ?? "" : "",
                                AgencyId = ""
                            });
                        }
                        if (entities.Count > 1)
                        {
                            alerts.Add(new ServiceAlert
                            {
                                Id = entity.GetProperty("id").GetString() ?? "",
                                Header = headerText ?? "",
                                Description = descriptionText ?? "",
                                ActivePeriods = activePeriods,
                                Entities = entities
                            });
                        }
                    }

                }

                Console.WriteLine($"Fetched {alerts.Count} service alerts from Legacy API");
                return alerts;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching service alerts: {ex.Message}");
                return new List<ServiceAlert>();
            }
        }
        public class ServiceAlert
        {
            public string Id { get; set; }
            public string Header { get; set; }
            public string Description { get; set; }
            public List<AlertPeriod> ActivePeriods { get; set; }
            public List<AlertEntity> Entities { get; set; }
        }

        public class AlertPeriod
        {
            public long Start { get; set; }
            public long End { get; set; }
        }

        public class AlertEntity
        {
            public string AgencyId { get; set; }
            public string RouteId { get; set; }
            public string StopId { get; set; }
        }

        //Function to check if a train LINE is running
        public bool isTrainRunning(TripStopAttributes trip, List<ServiceAlert> alerts, DateTime now, String agencyId = "")
        {
            //first check service date
            if (!DateTime.TryParse(trip.serviceDate, out DateTime serviceDate) || serviceDate.Date != now.Date)
                return false;

            //check pickup / dropoff
            if (trip.pickupType != 0 || trip.dropOffType != 0)
                return false;

            //check time window
            if (!DateTime.TryParse(trip.tripStartTime, out DateTime tripStart) || !DateTime.TryParse(trip.arrivalTime, out DateTime arrival))
                return false;

            if (now.TimeOfDay < tripStart.TimeOfDay || now.TimeOfDay > arrival.TimeOfDay)
                return false;

            //check service alert
            foreach (var alert in alerts)
            {
                if (isAlertActive(alert, now, trip.routeId, trip.stopId, agencyId))
                    return false;
            }



            return true;

        }


        private bool isAlertActive(
            ServiceAlert alert,
            DateTime now,
            string routeId,
            string stopId,
            string agencyId)
        {
            foreach (var period in alert.ActivePeriods)
            {
                DateTime start = DateTimeOffset.FromUnixTimeSeconds(period.Start).DateTime;
                DateTime end = DateTimeOffset.FromUnixTimeSeconds(period.End).DateTime;
                if (now >= start && now <= end)
                {
                    //check if alert affects the route/stop/agency
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

    }

    
  

}
