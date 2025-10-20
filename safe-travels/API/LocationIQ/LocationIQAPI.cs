using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace safe_travels.API.LocationIQ
{
    /// <summary>
    /// Provides methods to interact with the LocationIQ API for address autocomplete and geocoding.
    /// </summary>
    public class LocationIQAPI
    {
        private const string LocationIQApiKey = "pk.d257fa72d9927c4e2f43a9b7ad2bbf6a";
        private const string AutocompleteUrl = "https://us1.locationiq.com/v1/autocomplete.php";
        private const string GeocodeUrl = "https://us1.locationiq.com/v1/search.php";

        /// <summary>
        /// Gets address suggestions for the given input text.
        /// </summary>
        public async Task<List<LocationIQSuggestion>> GetAddressSuggestionsAsync(string query)
        {
            using var client = new HttpClient();
            var url = $"{AutocompleteUrl}?key={LocationIQApiKey}&q={Uri.EscapeDataString(query)}&limit=5&countrycodes=NZ&format=json";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<LocationIQSuggestion>>(content) ?? new();
        }

        /// <summary>
        /// Gets latitude and longitude for a given address string.
        /// </summary>
        public async Task<LocationIQGeocodeResult?> GeocodeAddressAsync(string address)
        {
            using var client = new HttpClient();
            var url = $"{GeocodeUrl}?key={LocationIQApiKey}&q={Uri.EscapeDataString(address)}&countrycodes=NZ&format=json";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<LocationIQGeocodeResult>>(content);
            return results?.Count > 0 ? results[0] : null;
        }
    }

    /// <summary>
    /// Represents a suggestion result from LocationIQ autocomplete.
    /// </summary>
    public class LocationIQSuggestion
    {
        public string display_name { get; set; } = string.Empty;
        public string lat { get; set; } = string.Empty;
        public string lon { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a geocode result from LocationIQ.
    /// </summary>
    public class LocationIQGeocodeResult
    {
        public string display_name { get; set; } = string.Empty;
        public string lat { get; set; } = string.Empty;
        public string lon { get; set; } = string.Empty;
    }
}