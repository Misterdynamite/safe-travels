namespace safe_travels.Models
{
    /// <summary>
    /// Represents a response containing trip stop data from the Auckland Transport API.
    /// </summary>
    public class TripStopResponse
    {
        /// <summary>
        /// Gets or sets the list of trip stop data.
        /// </summary>
        public required List<TripStopData> data { get; set; }
    }

    /// <summary>
    /// Represents the data for a specific trip stop.
    /// </summary>
    public class TripStopData
    {
        /// <summary>
        /// Gets or sets the type of the trip stop.
        /// </summary>
        public required string type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the trip stop.
        /// </summary>
        public required string id { get; set; }

        /// <summary>
        /// Gets or sets the attributes associated with the trip stop.
        /// </summary>
        public required TripStopAttributes attributes { get; set; }

        public override string ToString()
        {
            return $"TripStopData: [Type: {type}, Id: {id}, Attributes: {attributes}]";
        }
    }

    /// <summary>
    /// Represents the attributes of a trip stop, including timing and route information.
    /// </summary>
    public class TripStopAttributes
    {
        /// <summary>
        /// Gets or sets the arrival time at the stop.
        /// </summary>
        public required string arrivalTime { get; set; }

        /// <summary>
        /// Gets or sets the departure time from the stop.
        /// </summary>
        public required string departureTime { get; set; }

        /// <summary>
        /// Gets or sets the direction ID for the trip.
        /// </summary>
        public int directionId { get; set; }

        /// <summary>
        /// Gets or sets the drop-off type for the stop.
        /// </summary>
        public int dropOffType { get; set; }

        /// <summary>
        /// Gets or sets the pickup type for the stop.
        /// </summary>
        public int pickupType { get; set; }

        /// <summary>
        /// Gets or sets the route ID for the trip.
        /// </summary>
        public required string routeId { get; set; }

        /// <summary>
        /// Gets or sets the service date for the trip.
        /// </summary>
        public required string serviceDate { get; set; }

        /// <summary>
        /// Gets or sets the shape ID for the trip.
        /// </summary>
        public required string shapeId { get; set; }

        /// <summary>
        /// Gets or sets the head sign for the stop.
        /// </summary>
        public required string stopHeadSign { get; set; }

        /// <summary>
        /// Gets or sets the stop ID.
        /// </summary>
        public required string stopId { get; set; }

        /// <summary>
        /// Gets or sets the sequence number of the stop in the trip.
        /// </summary>
        public int stopSequence { get; set; }

        /// <summary>
        /// Gets or sets the trip ID.
        /// </summary>
        public required string tripId { get; set; }

        /// <summary>
        /// Gets or sets the start time of the trip.
        /// </summary>
        public required string tripStartTime { get; set; }

        public override string ToString()
        {
            return $"[ArrivalTime: {arrivalTime}, DepartureTime: {departureTime}, DirectionId: {directionId}, DropOffType: {dropOffType}, PickupType: {pickupType}, RouteId: {routeId}, ServiceDate: {serviceDate}, ShapeId: {shapeId}, StopHeadSign: {stopHeadSign}, StopId: {stopId}, StopSequence: {stopSequence}, TripId: {tripId}, TripStartTime: {tripStartTime}]";
        }
    }
}