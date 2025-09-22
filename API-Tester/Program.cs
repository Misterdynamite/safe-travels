using safe_travels.API.AucklandTransportAPI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AucklandTransportAPI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var stopsCalls = new StopsCalls();
            var results = await stopsCalls.GetStopsByName("Constellation Bus Station");
            Stop firstResult = results[0];
            Console.WriteLine($"Found {results.Count} stops matching 'Torbay'. First stop ID: {firstResult.stopId}, Name: {firstResult.stopName}");
            
            
            var stopTripCalls = new StopTripsCalls();
            var tripResults = await stopTripCalls.GetTripsByStopID(firstResult.stopId);
            Console.WriteLine($"Found {tripResults.Count} trips for stop {firstResult.stopName}");
        }
    }
}