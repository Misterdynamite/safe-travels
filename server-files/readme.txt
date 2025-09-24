Current API usage

# Get first 10 stops
curl "http://rest.kennedys.nz/api/stops?limit=10"

# Search for stops with "queen" in name
curl "http://rest.kennedys.nz/api/stops?search=queen"


# Get all trips that pass through stop "133"
curl "https://rest.kennedys.nz/api/stops/133/trips"

# Get trips through stop "133" on route "20503" only
curl "https://rest.kennedys.nz/api/stops/133/trips?route_id=20503"

# Get first 10 trips through stop "133"
curl "https://rest.kennedys.nz/api/stops/133/trips?limit=10"
