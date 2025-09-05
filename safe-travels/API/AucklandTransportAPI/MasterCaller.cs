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
        public async Task DemoStopToTripFlow(string stopNameInput)
        {
            StopsCalls stopsCaller = new StopsCalls();
            List<StopData> stops = await stopsCaller.GetStopsByName(stopNameInput);

            if (stops == null || stops.Count == 0)
            {
                Debug.WriteLine("No stops found for input: " + stopNameInput);
                return;
            }

            for (int i = 0; i < stops.Count; i++)
            {
                StopData stop = stops[i];
                Debug.WriteLine($"{i + 1}. {stop.attributes.stopName} (ID: {stop.attributes.stopId})");
            }

            // picks first stop bc search button doesnt exist yet
            StopData selectedStop = stops[0];
            Debug.WriteLine($"Selected stop: {selectedStop.attributes.stopName} (ID: {selectedStop.attributes.stopId})");
        }
    }
}
