using System.Text.Json;
using safe_travels.Models;

namespace safe_travels.API.AucklandTransportAPI.Legacy
{
    class LegacyServiceAlertsAPI
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
        public async Task<List<ServiceAlert>> GetServiceAlertsAsync(string stopIdInput)
        {
            try
            {
                //http client and set headers
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                //send a get request to the api
                var response = await client.GetAsync(apiURL); 
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                //parse json response
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                var alerts = new List<ServiceAlert>();
                //navigate to response then to entity array
                if (root.TryGetProperty("response", out var responseObj) &&
                    responseObj.TryGetProperty("entity", out var entityArray))
                {
                    //loop through every alert entity
                    foreach (var entity in entityArray.EnumerateArray())
                    {
                        //if no alert property, skip to next entity
                        if (!entity.TryGetProperty("alert", out var alertObj)) continue;
                        //get header and description text
                        var headerText = alertObj.GetProperty("header_text")
                            .GetProperty("translation")[0].GetProperty("text").GetString();
                        //get description text
                        var descriptionText = alertObj.GetProperty("description_text")
                            .GetProperty("translation")[0].GetProperty("text").GetString();
                        //get active periods (start and end times)
                        var activePeriods = new List<AlertPeriod>();
                        //loop through every active period
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
                        //extract the entities (stops/routes) affected by the alert

                        var entities = new List<AlertEntity>();
                        foreach (var informed in alertObj.GetProperty("informed_entity").EnumerateArray())
                        {
                            var stopId = informed.TryGetProperty("stop_id", out var stopIdProp) ? stopIdProp.GetString() ?? "" : "";
                            //filter by stopID input here
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
                        //only add alert if it has more than 1 entity (stop/route) affected
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
    }
}