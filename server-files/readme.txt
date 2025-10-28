# Auckland Transport GTFS API Server

A real-time transit information system for Auckland Transport that combines static GTFS schedule data with live vehicle positions, delays, and service alerts.

### cache_program.py
**GTFS Data Cacher** - Runs continuously in the background

- Automaticly ownloads AT's GTFS static schedule data
- Updates stops & routes every 12 hours from AT API
- Updates trips & stop_times every 24 hours from GTFS feed
- Stores all data in PostgreSQL database

### api_server.py
**REST API Server** - Serves transit data with automatic real-time calculation

- Provides stripped down returns for AT API data with data limits and less bloat
- Unlimmited calls - Not limited by a token count

### realtime_integration.py
- **Live Data Integration** - Connects to Auckland Transport's real-time feeds

- Automatic Real-time Data Replacement: Scheduled times are automatically replaced with live predictions
- Smart Data Fetching: Active trips get fresh data, future trips use efficient caching

## API Usage

### Stops
```bash
# Search for stops
curl "http://rest.kennedys.nz/api/stops?search=queen&per_page=10"

# Get specific stop with alerts
curl "http://rest.kennedys.nz/api/stops/133"

# Get upcoming trips at a stop (with automatic real-time delays)
curl "http://rest.kennedys.nz/api/stops/133/trips?limit=20"

# Get next departures with real-time predictions
curl "http://rest.kennedys.nz/api/stop_times/133?limit=10"

# Get alerts affecting a stop
curl "http://rest.kennedys.nz/api/stops/133/alerts"
```

### Trips
```bash
# Get trip details with live vehicle position
curl "http://rest.kennedys.nz/api/trips/1234567-20251026"

# Get all remaining stops for a trip (with real-time ETAs)
curl "http://rest.kennedys.nz/api/trips/1234567-20251026/stops"
```

### Routes
```bash
# Get service alerts for a route
curl "http://rest.kennedys.nz/api/routes/20503/alerts"
```

### Real-time Data (Direct Access)
```bash
# All trip updates (delays/cancellations)
curl "http://rest.kennedys.nz/api/realtime/trip-updates"

# All vehicle positions
curl "http://rest.kennedys.nz/api/realtime/vehicle-positions"

# All service alerts
curl "http://rest.kennedys.nz/api/realtime/service-alerts"
```

### System Stats
```bash
# Database and real-time statistics
curl "http://rest.kennedys.nz/api/stats"
```

