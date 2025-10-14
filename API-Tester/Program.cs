using API_Tester.AucklandTransportAPI;
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

            var stopId = "7128-d8460fc5";
            Console.WriteLine($"Fetching service alerts for stop ID: {stopId}");
            var api = new ServiceUpdates();
            var alerts = await api.GetServiceAlertsAsync(stopId);

            foreach (var alert in alerts)
            {
                Console.WriteLine($"{alert.Header}");
                Console.WriteLine($"Description: {alert.Description}");
                foreach (var entity in alert.Entities)
                {
                    Console.WriteLine($"Route: {entity.RouteId}, Stop: {entity.StopId}");
                }
                Console.WriteLine("-----");
            }

        }


    }
}
