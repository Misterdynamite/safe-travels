using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Maui.Storage;
using safe_travels.API.AucklandTransportAPI;

namespace safe_travels.Utilities
{
    internal class StorageManager
    {
        private const string FavoriteStopsKey = "favorite_stops";

        public static void SaveFavoriteStops(List<Stop> stops)
        {
            string json = JsonSerializer.Serialize(stops);
            Preferences.Set(FavoriteStopsKey, json);
        }

        public static List<Stop> LoadFavoriteStops()
        {
            string json = Preferences.Get(FavoriteStopsKey, "[]");
            return JsonSerializer.Deserialize<List<Stop>>(json) ?? new List<Stop>();
        }

        public static void AddFavoriteStop(Stop stop)
        {
            var stops = LoadFavoriteStops();
            if (!stops.Any(s => s.stopId == stop.stopId))
            {
                stops.Add(stop);
                SaveFavoriteStops(stops);
            }
        }

        public static void RemoveFavoriteStop(string stopId)
        {
            var stops = LoadFavoriteStops();
            stops.RemoveAll(s => s.stopId == stopId);
            SaveFavoriteStops(stops);
        }

        public static bool IsFavorite(string stopId)
        {
            var stops = LoadFavoriteStops();
            return stops.Any(s => s.stopId == stopId);
        }
    }
}
