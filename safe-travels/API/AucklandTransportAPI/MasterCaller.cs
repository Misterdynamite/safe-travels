using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using safe_travels.API.AucklandTransportAPI.Legacy;

namespace safe_travels.API.AucklandTransportAPI
{
    internal class MasterCaller
    {
        private readonly StopsCalls _stopsCaller;
        private readonly LegacyStopsCalls _legacyStopsCaller;
        private readonly StopTripsCalls _stopTripsCaller;
        private readonly LegacyStopTripsCalls _legacyStopTripsCaller;
        private readonly TripCalls _tripCaller;
        private readonly LegacyTripCalls _legacyTripCaller;

        public MasterCaller()
        {
            _stopsCaller = new StopsCalls();
            _legacyStopsCaller = new LegacyStopsCalls();
            _stopTripsCaller = new StopTripsCalls();
            _legacyStopTripsCaller = new LegacyStopTripsCalls();
            _tripCaller = new TripCalls();
            _legacyTripCaller = new LegacyTripCalls();
        }

        public async Task<List<Stop>> FetchStopsNearUser(double lat, double longi)
        {
            List<Stop> stops = new List<Stop>();

            try
            {
                Debug.WriteLine("Attempting to fetch stops using new API...");
                stops = await _stopsCaller.GetStopsByProximity(lat, longi, 1000);
                
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
                    stops = await _legacyStopsCaller.GetStopsByProximity(lat, longi, 1000);
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
        public async Task<List<Stop>> DemoStopToTripFlow(string stopNameInput)
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


        public async Task DemoTripIdData(string tripId)
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
        /// Demonstrates fetching trips for a specific stop with fallback to legacy API
        /// </summary>
        /// <param name="stopId">The stop ID to get trips for</param>
        /// <returns>A list of TripStopResponse objects</returns>
        public async Task<List<TripStopResponse>> GetTripsForStop(string stopId)
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
