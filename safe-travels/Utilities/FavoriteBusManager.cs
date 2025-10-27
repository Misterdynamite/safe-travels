using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;
using safe_travels.Models;

namespace safe_travels.Utilities
{
    internal static class FavoriteBusManager
    {
        private const string FavoriteStopsKey = "favorite_stops";

        public static void SaveFavoriteStops(List<FavoriteStop> stops)
        {
            var options = new JsonSerializerOptions { WriteIndented = false };
            string json = JsonSerializer.Serialize(stops, options);
            Preferences.Set(FavoriteStopsKey, json);
        }

        public static List<FavoriteStop> LoadFavoriteStops()
        {
            string json = Preferences.Get(FavoriteStopsKey, "[]");
            try
            {
                return JsonSerializer.Deserialize<List<FavoriteStop>>(json) ?? new List<FavoriteStop>();
            }
            catch
            {
                Preferences.Set(FavoriteStopsKey, "[]");
                return new List<FavoriteStop>();
            }
        }

        public static void AddFavoriteStop(FavoriteStop stop)
        {
            var stops = LoadFavoriteStops();
            var existing = stops.FirstOrDefault(s => s.stopId == stop.stopId);
            if (existing == null)
            {
                stops.Add(stop);
            }
            else
            {
                // update fields
                existing.stopName = stop.stopName;
                existing.stopLat = stop.stopLat;
                existing.stopLong = stop.stopLong;
                existing.Nickname = stop.Nickname ?? existing.Nickname;
            }
            SaveFavoriteStops(stops);
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
