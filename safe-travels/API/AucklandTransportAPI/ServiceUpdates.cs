using safe_travels.API.AucklandTransportAPI;
using safe_travels.API.AucklandTransportAPI.Legacy;
using System.Text.Json;
using System.Diagnostics;

namespace API_Tester.AucklandTransportAPI
{
    /// <summary>
    /// Provides methods to fetch service alerts and check service status,
    /// with automatic fallback from new API to legacy API if needed.
    /// </summary>
    class ServiceUpdates
    {
        private static readonly ServiceAlertsAPI _serviceAlertsCaller = new ServiceAlertsAPI();
        private static readonly LegacyServiceAlertsAPI _legacyServiceAlertsCaller = new LegacyServiceAlertsAPI();

        /// <summary>
        /// Gets service alerts for a specific stop ID using the new Auckland Transport API,
        /// with fallback to the legacy API if necessary.
        /// </summary>
        /// <param name="stopId">The stop ID to get alerts for.</param>
        /// <returns>A list of service alerts affecting the specified stop.</returns>
        public async Task<List<ServiceAlert>> GetServiceAlertsByStopAsync(string stopId)
        {
            List<ServiceAlert> alerts = new List<ServiceAlert>();

            try
            {
                Debug.WriteLine($"Attempting to fetch service alerts for stop {stopId} using new API...");
                alerts = await _serviceAlertsCaller.GetAlertsByStopIdAsync(stopId);
                
                if (alerts != null && alerts.Count > 0)
                {
                    Debug.WriteLine($"Successfully fetched {alerts.Count} alerts using new API");
                }
                else
                {
                    throw new Exception("New API returned no results");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"New API failed: {ex.Message}. Falling back to legacy API...");
                
                try
                {
                    var legacyAlerts = await _legacyServiceAlertsCaller.GetServiceAlertsAsync(stopId);
                    alerts = ConvertLegacyAlerts(legacyAlerts);
                    
                    if (alerts != null && alerts.Count > 0)
                    {
                        Debug.WriteLine($"Successfully fetched {alerts.Count} alerts using legacy API");
                    }
                    else
                    {
                        Debug.WriteLine("Legacy API also returned no results");
                        alerts = new List<ServiceAlert>();
                    }
                }
                catch (Exception legacyEx)
                {
                    Debug.WriteLine($"Legacy API also failed: {legacyEx.Message}");
                    alerts = new List<ServiceAlert>();
                }
            }

            return alerts;
        }

        /// <summary>
        /// Gets service alerts for a specific route ID using the new Auckland Transport API,
        /// with fallback to the legacy API if necessary.
        /// </summary>
        /// <param name="routeId">The route ID to get alerts for.</param>
        /// <returns>A list of service alerts affecting the specified route.</returns>
        public async Task<List<ServiceAlert>> GetServiceAlertsByRouteAsync(string routeId)
        {
            List<ServiceAlert> alerts = new List<ServiceAlert>();

            try
            {
                Debug.WriteLine($"Attempting to fetch service alerts for route {routeId} using new API...");
                alerts = await _serviceAlertsCaller.GetAlertsByRouteIdAsync(routeId);
                
                if (alerts != null && alerts.Count > 0)
                {
                    Debug.WriteLine($"Successfully fetched {alerts.Count} alerts using new API");
                }
                else
                {
                    throw new Exception("New API returned no results");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"New API failed: {ex.Message}. Falling back to legacy API...");
                
                try
                {
                    // Legacy API doesn't have route-specific endpoint, so get all alerts
                    var legacyAlerts = await _legacyServiceAlertsCaller.GetServiceAlertsAsync("");
                    alerts = ConvertLegacyAlerts(legacyAlerts);
                    
                    // Filter by route ID
                    alerts = alerts.Where(a => a.Entities.Any(e => e.RouteId == routeId)).ToList();
                    
                    if (alerts != null && alerts.Count > 0)
                    {
                        Debug.WriteLine($"Successfully fetched {alerts.Count} alerts using legacy API");
                    }
                    else
                    {
                        Debug.WriteLine("Legacy API also returned no results");
                        alerts = new List<ServiceAlert>();
                    }
                }
                catch (Exception legacyEx)
                {
                    Debug.WriteLine($"Legacy API also failed: {legacyEx.Message}");
                    alerts = new List<ServiceAlert>();
                }
            }

            return alerts;
        }

        /// <summary>
        /// Converts legacy service alerts to the new ServiceAlert format.
        /// </summary>
        private List<ServiceAlert> ConvertLegacyAlerts(List<LegacyServiceAlertsAPI.ServiceAlert> legacyAlerts)
        {
            var alerts = new List<ServiceAlert>();
            
            foreach (var legacy in legacyAlerts)
            {
                var alert = new ServiceAlert
                {
                    Id = legacy.Id,
                    Header = legacy.Header,
                    Description = legacy.Description,
                    ActivePeriods = new List<AlertPeriod>(),
                    Entities = new List<AlertEntity>()
                };

                // Convert active periods
                foreach (var period in legacy.ActivePeriods)
                {
                    alert.ActivePeriods.Add(new AlertPeriod
                    {
                        Start = period.Start,
                        End = period.End
                    });
                }

                // Convert entities
                foreach (var entity in legacy.Entities)
                {
                    alert.Entities.Add(new AlertEntity
                    {
                        AgencyId = entity.AgencyId,
                        RouteId = entity.RouteId,
                        StopId = entity.StopId
                    });
                }

                alerts.Add(alert);
            }

            return alerts;
        }

        /// <summary>
        /// Checks if a service (trip) is currently running based on service alerts and schedule.
        /// </summary>
        /// <param name="trip">The trip stop attributes to check.</param>
        /// <param name="alerts">The list of service alerts to check against.</param>
        /// <param name="now">The current date/time.</param>
        /// <param name="agencyId">The agency ID (optional).</param>
        /// <returns>True if the service is running, false otherwise.</returns>
        public bool IsServiceRunning(TripStopAttributes trip, List<ServiceAlert> alerts, DateTime now, string agencyId = "")
        {
            return _serviceAlertsCaller.IsServiceRunning(trip, alerts, now, agencyId);
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
        public bool IsAlertActive(ServiceAlert alert, DateTime now, string routeId = "", string stopId = "", string agencyId = "")
        {
            return _serviceAlertsCaller.IsAlertActive(alert, now, routeId, stopId, agencyId);
        }
    }
}
