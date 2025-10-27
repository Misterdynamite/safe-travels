using System;
using System.Diagnostics;

namespace safe_travels.Utilities
{
    /// <summary>
    /// Provides formatting utilities for countdown timers in the app.
    /// </summary>
    public static class CountdownFormatter
    {
        /// <summary>
        /// Calculates minutes until a specified arrival time.
        /// </summary>
        /// <param name="arrivalTime">The arrival time string.</param>
        /// <param name="tripStartTime">The trip start time string.</param>
        /// <param name="serviceDate">The service date string.</param>
        /// <returns>Minutes until arrival, or null if trip hasn't started or already passed.</returns>
        public static int? CalculateMinutesUntilArrival(string arrivalTime, string tripStartTime, string serviceDate)
        {
            try
            {
                // Parse the arrival time - handle multiple formats
                if (string.IsNullOrEmpty(arrivalTime))
                {
                    Debug.WriteLine("Arrival time is null or empty");
                    return null;
                }

                DateTime arrival;

                // Try parsing as a full DateTime first
                if (DateTime.TryParse(arrivalTime, out arrival))
                {
                    // Success - use as-is
                }
                // If that fails, try parsing as time only (HH:mm:ss) and combine with service date
                else if (TimeSpan.TryParse(arrivalTime, out TimeSpan arrivalTimeOfDay))
                {
                    DateTime serviceDay = DateTime.Today;
                    if (!string.IsNullOrEmpty(serviceDate))
                    {
                        // Try parsing YYYYMMDD format (e.g., "20251026")
                        if (serviceDate.Length == 8 && DateTime.TryParseExact(serviceDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime parsedServiceDate))
                        {
                            serviceDay = parsedServiceDate.Date;
                        }
                        // Fallback to standard parsing
                        else if (DateTime.TryParse(serviceDate, out DateTime fallbackDate))
                        {
                            serviceDay = fallbackDate.Date;
                        }
                    }
                    arrival = serviceDay.Add(arrivalTimeOfDay);
                }
                else
                {
                    Debug.WriteLine($"Could not parse arrival time: '{arrivalTime}'");
                    return null;
                }

                DateTime now = DateTime.Now;

                Debug.WriteLine($"[Countdown] Checking trip: Arrival={arrivalTime}, TripStart={tripStartTime}, ServiceDate={serviceDate}");
                Debug.WriteLine($"[Countdown] Parsed arrival DateTime: {arrival}, Current time: {now}");

                // Check if we have a valid trip start time
                bool hasTripStarted = true; // Default to true if we can't parse start time

                if (!string.IsNullOrEmpty(tripStartTime))
                {
                    DateTime startTime;

                    // Try parsing as DateTime
                    if (DateTime.TryParse(tripStartTime, out startTime))
                    {
                        hasTripStarted = startTime <= now;
                    }
                    // Try parsing as TimeSpan and combine with service date
                    else if (TimeSpan.TryParse(tripStartTime, out TimeSpan startTimeOfDay))
                    {
                        DateTime serviceDay = DateTime.Today;
                        if (!string.IsNullOrEmpty(serviceDate))
                        {
                            // Try parsing YYYYMMDD format (e.g., "20251026")
                            if (serviceDate.Length == 8 && DateTime.TryParseExact(serviceDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime parsedServiceDate))
                            {
                                serviceDay = parsedServiceDate.Date;
                            }
                            // Fallback to standard parsing
                            else if (DateTime.TryParse(serviceDate, out DateTime fallbackDate))
                            {
                                serviceDay = fallbackDate.Date;
                            }
                        }
                        startTime = serviceDay.Add(startTimeOfDay);
                        hasTripStarted = startTime <= now;
                    }
                }

                if (!hasTripStarted)
                {
                    Debug.WriteLine($"[Countdown] SKIPPED: Trip hasn't started yet");
                    return null;
                }

                // Calculate minutes until arrival
                TimeSpan timeUntilArrival = arrival - now;

                // Return null if the bus has already arrived (negative time)
                if (timeUntilArrival.TotalMinutes < 0)
                {
                    Debug.WriteLine($"[Countdown] SKIPPED: Bus already arrived ({timeUntilArrival.TotalMinutes:F1} mins ago)");
                    return null;
                }

                // If less than 30 seconds, return 0 to trigger "NOW"
                if (timeUntilArrival.TotalSeconds <= 30)
                {
                    Debug.WriteLine($"[Countdown] NOW: {timeUntilArrival.TotalSeconds:F0} seconds until arrival");
                    return 0;
                }

                int mins = (int)Math.Ceiling(timeUntilArrival.TotalMinutes);
                Debug.WriteLine($"[Countdown] SUCCESS: {mins} mins until arrival");
                return mins;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating minutes: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Formats countdown for bus arrivals at stops (used in BusDetailPage).
        /// </summary>
        /// <param name="minutes">Minutes until arrival.</param>
        /// <returns>Formatted string like "NOW", "1 min", "5 mins".</returns>
        public static string FormatBusArrival(int? minutes)
        {
            try
            {
                if (minutes.HasValue)
                {
                    if (minutes.Value == 0)
                    {
                        return "NOW";
                    }
                    else if (minutes.Value == 1)
                    {
                        return "1 min";
                    }
                    else
                    {
                        return $"{minutes.Value} mins";
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting bus arrival: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Formats countdown for hopping off at stops (used in TripStopsPage).
        /// </summary>
        /// <param name="minutes">Minutes until arrival.</param>
        /// <returns>Formatted string like "Hop off\nNOW", "Hop off in:\n1 min", etc.</returns>
        public static string FormatHopOff(int? minutes)
        {
            try
            {
                if (minutes.HasValue)
                {
                    if (minutes.Value == 0)
                    {
                        return "Hop off\nNOW";
                    }
                    else if (minutes.Value == 1)
                    {
                        return "Hop off in:\n1 min";
                    }
                    else
                    {
                        return $"Hop off in:\n{minutes.Value} mins";
                    }
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting hop off countdown: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
