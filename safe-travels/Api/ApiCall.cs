using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace safe_travels
{
    class ApiCall
    {
        private static readonly string ApiUrl = "https://api.at.govt.nz/gtfs/v3/stops";
        // Replace with your actual subscription key
        private static readonly string SubscriptionKey = "7b9ddef12bf5416fad6feff0800acd14";

        public async Task<List<StopData>> testTask(string stopnameinput)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
                    HttpResponseMessage response = await client.GetAsync(ApiUrl);

                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync();
                    ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(content);

                    if (apiResponse?.Data == null)
                        return new List<StopData>(); //return empty list if api returns nothing

                    List<StopData> filteredStops = apiResponse.Data
                           .Where(stop => !string.IsNullOrEmpty(stop.attributes.stop_name) &&
                                          stop.attributes.stop_name.IndexOf(stopnameinput, StringComparison.OrdinalIgnoreCase) >= 0)
                           .ToList();

                    return filteredStops;

                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log them)
                Console.WriteLine($"An error occurred: {ex.Message}");
                return new List<StopData>(); // Return an empty list in case of error
            }
        }
    }

    public class ApiResponse
    {
        [JsonProperty("data")]
        public List<StopData> Data { get; set; }
    }

    public class StopData
    {
        public string type { get; set; }
        public string id { get; set; }
        public StopAttributes attributes { get; set; }
    }

    public class  StopAttributes
    {
        public string stop_id { get; set; }
        public string stop_name { get; set; }
        public double stop_lat { get; set; }

        public double stop_lon { get; set; }    
    }

}
