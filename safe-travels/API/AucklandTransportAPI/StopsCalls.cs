using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace safe_travels.API.AucklandTransportAPI
{
    class StopsCalls
    {
        private static readonly string apiURL = "https://api.at.govt.nz/gtfs/v3/stops";
        private static readonly string subscriptionKey = "7b9ddef12bf5416fad6feff0800acd14";

        public async Task<List<StopData>> GetStopsByName(string stopNameInput)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var response = await client.GetAsync(apiURL);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(content);

                if (apiResponse?.data == null)
                    return new List<StopData>();

                return apiResponse.data
                    .Where(stop => !string.IsNullOrEmpty(stop.attributes.stopName) &&
                                   stop.attributes.stopName.IndexOf(stopNameInput, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return new List<StopData>();
            }
        }
    }

    public class ApiResponse
    {
        [JsonProperty("data")]
        public List<StopData> data { get; set; }
    }

    public class StopData
    {
        public string type { get; set; }
        public string id { get; set; }
        public StopAttributes attributes { get; set; }
    }

    public class StopAttributes
    {
        public string stopId { get; set; }
        public string stopName { get; set; }
        public double stopLat { get; set; }
        public double stopLong { get; set; }
    }

}
