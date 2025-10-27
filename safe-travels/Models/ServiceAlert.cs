namespace safe_travels.Models
{
    /// <summary>
    /// Represents a service alert from the Auckland Transport API.
    /// </summary>
    public class ServiceAlert
    {
        /// <summary>
        /// Gets or sets the unique identifier for the alert.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Gets or sets the header/title of the alert.
        /// </summary>
        public required string Header { get; set; }

        /// <summary>
        /// Gets or sets the detailed description of the alert.
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Gets or sets the list of time periods when this alert is active.
        /// </summary>
        public required List<AlertPeriod> ActivePeriods { get; set; }

        /// <summary>
        /// Gets or sets the list of entities (stops, routes, agencies) affected by this alert.
        /// </summary>
        public required List<AlertEntity> Entities { get; set; }
    }

    /// <summary>
    /// Represents a time period during which an alert is active.
    /// </summary>
    public class AlertPeriod
    {
        /// <summary>
        /// Gets or sets the start time of the alert period (Unix timestamp in seconds).
        /// </summary>
        public long Start { get; set; }

        /// <summary>
        /// Gets or sets the end time of the alert period (Unix timestamp in seconds).
        /// </summary>
        public long End { get; set; }
    }

    /// <summary>
    /// Represents an entity (stop, route, or agency) affected by an alert.
    /// </summary>
    public class AlertEntity
    {
        /// <summary>
        /// Gets or sets the agency ID affected by the alert.
        /// </summary>
        public required string AgencyId { get; set; }

        /// <summary>
        /// Gets or sets the route ID affected by the alert.
        /// </summary>
        public required string RouteId { get; set; }

        /// <summary>
        /// Gets or sets the stop ID affected by the alert.
        /// </summary>
        public required string StopId { get; set; }
    }
}