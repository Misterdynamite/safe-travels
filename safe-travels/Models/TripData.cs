namespace safe_travels.Models
{
    /// <summary>
    /// Represents trip data from the Auckland Transport API.
    /// </summary>
    public class TripData
    {
        public string type { get; set; } = string.Empty;
        public string id { get; set; } = string.Empty;
        public TripAttributes attributes { get; set; } = new TripAttributes();
    }

    /// <summary>
    /// Represents the attributes of a trip.
    /// </summary>
    public class TripAttributes
    {
        public string tripId { get; set; } = string.Empty;
        public string tripHeadsign { get; set; } = string.Empty;
        public string tripStartTime { get; set; } = string.Empty;
        public string routeId { get; set; } = string.Empty;
        public string serviceDate { get; set; } = string.Empty;
        public string stopHeadsign { get; set; } = string.Empty;
        public int directionId { get; set; }
        public string shapeId { get; set; } = string.Empty;
    }
}