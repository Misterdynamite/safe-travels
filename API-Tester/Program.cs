using System;
using System.Threading.Tasks;
using AucklandTransportAPI;

class Program
{
    static async Task Main(string[] args)
    {
        var stopsCalls = new StopsCalls();
        var results = await stopsCalls.GetStopsByName("Torbay Shops");
        StopData firstResult = results[0];
        Console.WriteLine($"Found {results.Count} stops matching 'Torbay'. First stop ID: {firstResult.attributes.stopId}, Name: {firstResult.attributes.stopName}");
        var stopTripCalls = new StopTripsCalls();
        var tripResults = await stopTripCalls.GetTripsByStopID(firstResult.attributes.stopId);
        Console.WriteLine($"Found {tripResults.Count} trips for stop {firstResult.attributes.stopName}");
    }
}