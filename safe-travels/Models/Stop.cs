namespace safe_travels.Models
{
    /// <summary>
    /// Represents a stop returned by the Auckland Transport API.
    /// </summary>
    public class Stop
    {
        /// <summary>
        /// Gets or sets the type of the stop.
        /// </summary>
        public required string type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the stop.
        /// </summary>
        public required string id { get; set; }

        /// <summary>
        /// Gets or sets the stop ID.
        /// </summary>
        public required string stopId { get; set; }

        /// <summary>
        /// Gets or sets the name of the stop.
        /// </summary>
        public required string stopName { get; set; }

        /// <summary>
        /// Gets or sets the latitude of the stop.
        /// </summary>
        public double stopLat { get; set; }

        /// <summary>
        /// Gets or sets the longitude of the stop.
        /// </summary>
        public double stopLong { get; set; }
    }
}