using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using safe_travels.API.AucklandTransportAPI.Legacy;

namespace safe_travels.API.AucklandTransportAPI
{
    /// <summary>
    /// Provides static methods to interact with Auckland Transport APIs for stops and trips,
    /// including fallback to legacy APIs if the primary API fails.
    /// </summary>  
    internal static class AucklandTransportAPIClient
    {
        private static readonly StopsAPI _stopsCaller = new StopsAPI();
        private static readonly LegacyStopsAPI _legacyStopsCaller = new LegacyStopsAPI();
        private static readonly InboundTripsAPI _stopTripsCaller = new InboundTripsAPI();
        private static readonly LegacyInboundTripsAPI _legacyStopTripsCaller = new LegacyInboundTripsAPI();
        private static readonly TripsAPI _tripCaller = new TripsAPI();
        private static readonly LegacyTripsAPI _legacyTripCaller = new LegacyTripsAPI();

        /// <summary>
        /// Fetches a list of stops near the user's location using the new API, 
        /// with fallback to the legacy API if necessary.
        /// </summary>
        /// <param name="lat">The latitude of the user's location.</param>
        /// <param name="longi">The longitude of the user's location.</param>
        /// <returns>
        /// A list of <see cref="Stop"/> objects within 1000 meters of the specified location.
        /// Returns an empty list if no stops are found or both APIs fail.
        /// </returns>
        public static async Task<List<Stop>> FetchStopsNearUser(double lat, double longi, int meters = 1000)
        {
            List<Stop> stops = new List<Stop>();

            try
            {
                Debug.WriteLine("Attempting to fetch stops using new API...");
                stops = await _stopsCaller.GetStopsByProximity(lat, longi, meters);
                
                if (stops != null && stops.Count > 0)
                {
                    Debug.WriteLine($"Successfully fetched {stops.Count} stops using new API");
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
                    stops = await _legacyStopsCaller.GetStopsByProximity(lat, longi, meters);
                    if (stops != null && stops.Count > 0)
                    {
                        Debug.WriteLine($"Successfully fetched {stops.Count} stops using legacy API");
                    }
                    else
                    {
                        Debug.WriteLine("Legacy API also returned no results");
                        stops = new List<Stop>(); 
                    }
                }
                catch (Exception legacyEx)
                {
                    Debug.WriteLine($"Legacy API also failed: {legacyEx.Message}");
                    stops = new List<Stop>(); 
                }
            }

            for (int i = 0; i < stops.Count; i++)
            {
                Stop stop = stops[i];
                Debug.WriteLine($"{i + 1}. {stop.stopName} (ID: {stop.stopId})");
            }

            return stops;
        }

        /// <summary>
        /// Demonstrates fetching stops by name and selecting the first result.
        /// Falls back to the legacy API if the new API fails.
        /// </summary>
        /// <param name="stopNameInput">The name or partial name of the stop to search for.</param>
        /// <returns>
        /// A list of <see cref="Stop"/> objects matching the input name.
        /// Returns an empty list if no stops are found or both APIs fail.
        /// </returns>
        public static async Task<List<Stop>> DemoStopToTripFlow(string stopNameInput)
        {
            List<Stop> stops = new List<Stop>();

            try
            {
                Debug.WriteLine("Attempting to fetch stops by name using new API...");
                stops = await _stopsCaller.GetStopsByName(stopNameInput);
                
                if (stops != null && stops.Count > 0)
                {
                    Debug.WriteLine($"Successfully fetched {stops.Count} stops using new API");
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
                    stops = await _legacyStopsCaller.GetStopsByName(stopNameInput);
                    if (stops != null && stops.Count > 0)
                    {
                        Debug.WriteLine($"Successfully fetched {stops.Count} stops using legacy API");
                    }
                    else
                    {
                        Debug.WriteLine("Legacy API also returned no results");
                        stops = new List<Stop>(); 
                    }
                }
                catch (Exception legacyEx)
                {
                    Debug.WriteLine($"Legacy API also failed: {legacyEx.Message}");
                    stops = new List<Stop>(); 
                }
            }

            if (stops.Count == 0)
            {
                Debug.WriteLine("No stops found for input: " + stopNameInput);
                return stops;
            }

            for (int i = 0; i < stops.Count; i++)
            {
                Stop stop = stops[i];
                Debug.WriteLine($"{i + 1}. {stop.stopName} (ID: {stop.stopId})");
            }

            // picks first stop bc search button doesnt exist yet
            Stop selectedStop = stops[0];
            Debug.WriteLine($"Selected stop: {selectedStop.stopName} (ID: {selectedStop.stopId})");
            return stops;
        }

        /// <summary>
        /// Demonstrates fetching trip data by trip ID using the new API,
        /// with fallback to the legacy API if necessary.
        /// </summary>
        /// <param name="tripId">The trip ID to search for.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// Outputs trip details to the debug log.
        /// </returns>
        public static async Task DemoTripIdData(string tripId)
        {
            List<TripData> trips = new List<TripData>();

            try
            {
                Debug.WriteLine("Attempting to fetch trip data using new API...");
                trips = await _tripCaller.GetTripbyTripIDMatch(tripId);
                
                if (trips != null && trips.Count > 0)
                {
                    Debug.WriteLine($"Successfully fetched {trips.Count} trips using new API");
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
                    trips = await _legacyTripCaller.GetTripbyTripIDMatch(tripId);
                    if (trips != null && trips.Count > 0)
                    {
                        Debug.WriteLine($"Successfully fetched {trips.Count} trips using legacy API");
                    }
                    else
                    {
                        Debug.WriteLine("Legacy API also returned no results");
                        trips = new List<TripData>();
                    }
                }
                catch (Exception legacyEx)
                {
                    Debug.WriteLine($"Legacy API also failed: {legacyEx.Message}");
                    trips = new List<TripData>();
                }
            }

            if (trips.Count == 0)
            {
                Debug.WriteLine("No trips found for trip ID: " + tripId);
                return;
            }

            for (int i = 0; i < trips.Count; i++)
            {
                TripData trip = trips[i];
                Debug.WriteLine($"{i + 1}. Trip ID: {trip.id}, Route ID: {trip.attributes.routeId}, Start Time: {trip.attributes.tripStartTime}, Bus HeadSign: {trip.attributes.stopHeadsign} ");
            }
            
            TripData selectedTrip = trips[0];
            Debug.WriteLine($"Selected Trip ID: {selectedTrip.id}, Route ID: {selectedTrip.attributes.routeId}, Start Time: {selectedTrip.attributes.tripStartTime}, Bus HeadSign: {selectedTrip.attributes.stopHeadsign} ");
        }

        /// <summary>
        /// Fetches trips for a specific stop using the new API, 
        /// with fallback to the legacy API if necessary.
        /// </summary>
        /// <param name="stopId">The stop ID to get trips for.</param>
        /// <returns>
        /// A list of <see cref="TripStopResponse"/> objects containing trip data for the specified stop.
        /// Returns an empty list if no trips are found or both APIs fail.
        /// </returns>
        public static async Task<List<TripStopResponse>> GetTripsForStop(string stopId)
        {
            List<TripStopResponse> trips = new List<TripStopResponse>();

            try
            {
                Debug.WriteLine($"Attempting to fetch trips for stop {stopId} using new API...");
                trips = await _stopTripsCaller.GetTripsByStopID(stopId);
                
                if (trips != null && trips.Count > 0)
                {
                    Debug.WriteLine($"Successfully fetched {trips.Count} trip responses using new API");
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
                    trips = await _legacyStopTripsCaller.GetTripsByStopID(stopId);
                    if (trips != null && trips.Count > 0)
                    {
                        Debug.WriteLine($"Successfully fetched {trips.Count} trip responses using legacy API");
                    }
                    else
                    {
                        Debug.WriteLine("Legacy API also returned no results");
                        trips = new List<TripStopResponse>(); 
                    }
                }
                catch (Exception legacyEx)
                {
                    Debug.WriteLine($"Legacy API also failed: {legacyEx.Message}");
                    trips = new List<TripStopResponse>();
                }
            }

            if (trips.Count == 0)
            {
                Debug.WriteLine($"No trips found for stop ID: {stopId}");
            }
            else
            {
                foreach (var tripResponse in trips)
                {
                    foreach (var tripData in tripResponse.data)
                    {
                        Debug.WriteLine($"Trip ID: {tripData.attributes.tripId}, Route: {tripData.attributes.routeId}, Departure: {tripData.attributes.departureTime}");
                    }
                }
            }

            return trips;
        }
    }
}
