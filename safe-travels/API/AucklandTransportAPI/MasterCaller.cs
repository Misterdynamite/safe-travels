using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace safe_travels.API.AucklandTransportAPI
{
    internal class MasterCaller
    {
        public async Task<List<Stop>> DemoStopToTripFlow(string stopNameInput)
        {
            StopsCalls stopsCaller = new StopsCalls();
            List<Stop> stops = await stopsCaller.GetStopsByName(stopNameInput);

            if (stops == null || stops.Count == 0)
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
            //use stopId to get trip data
            TripCalls tripCaller = new TripCalls();
            List<TripData> trips = await tripCaller.GetTripbyTripIDMatch(tripId);

            if (trips == null || trips.Count == 0)
            {
                Debug.WriteLine("No trips found for trip ID: " + tripId);
                return;
            }

            for ( int i = 0; i < trips.Count; i++)
            {
                TripData trip = trips[i];
                Debug.WriteLine($"{i + 1}. Trip ID: {trip.id}, Route ID: {trip.attributes.routeId}, Start Time: {trip.attributes.tripStartTime}, Buss HeadSign: {trip.attributes.stopHeadsign} ");
            }
            TripData selectedTrip = trips[0];
            Debug.WriteLine($"Selected Trip ID: {selectedTrip.id}, Route ID: {selectedTrip.attributes.routeId}, Start Time: {selectedTrip.attributes.tripStartTime}, Buss HeadSign: {selectedTrip.attributes.stopHeadsign} ");

        }
    }
}
