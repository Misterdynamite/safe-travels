from flask import Flask, jsonify, request
import psycopg2
from datetime import datetime
import json
import sys

app = Flask(__name__)

# Database configuration
DB_CONFIG = {
    "dbname": "safetravels",
    "user": "remote",
    "password": "safetravels",
    "host": "localhost",
    "port": "5432"
}

def log(message):
    """Simple logging function"""
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{timestamp}] {message}")
    sys.stdout.flush()

def get_db_connection():
    """Get database connection"""
    return psycopg2.connect(**DB_CONFIG)

def execute_query(query, params=None):
    """Execute database query and return results with better error handling"""
    conn = None
    cur = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()
        cur.execute(query, params or [])
        
        # Check if it's a SELECT query
        if query.strip().upper().startswith('SELECT'):
            columns = [desc[0] for desc in cur.description]
            rows = cur.fetchall()
            result = []
            for row in rows:
                result.append(dict(zip(columns, row)))
            return result
        else:
            conn.commit()
            return []
    except psycopg2.Error as e:
        log(f"Database error: {e}")
        return None
    except Exception as e:
        log(f"Unexpected error in execute_query: {e}")
        return None
    finally:
        if cur:
            cur.close()
        if conn:
            conn.close()

# Root endpoint
@app.route('/')
def root():
    """API information"""
    return jsonify({
        "message": "Auckland Transport GTFS REST API",
        "version": "2.0",
        "description": "Hybrid API serving real-time data from AT GTFS API and comprehensive trip data from GTFS static files",
        "data_sources": {
            "stops": "AT GTFS API (updated every 12 hours)",
            "routes": "AT GTFS API (updated every 12 hours)", 
            "trips": "GTFS Static File (updated every 24 hours)",
            "stop_times": "GTFS Static File (updated every 24 hours)"
        },
        "endpoints": {
            "stops": {
                "get_all": "/api/stops",
                "get_one": "/api/stops/{stop_id}",
                "description": "Bus stop locations and details"
            },
            "routes": {
                "get_all": "/api/routes", 
                "get_one": "/api/routes/{route_id}",
                "description": "Bus route information"
            },
            "trips": {
                "get_all": "/api/trips",
                "get_one": "/api/trips/{trip_id}",
                "description": "Trip schedules and details"
            },
            "stop_times": {
                "get_all": "/api/stop_times",
                "get_for_stop": "/api/stop_times/{stop_id}",
                "description": "Scheduled arrival/departure times"
            }
        },
        "stats": "/api/stats",
        "timestamp": datetime.now().isoformat()
    })

# API Stats
@app.route('/api/stats')
def get_stats():
    """Get database statistics"""
    try:
        stats = {}
        
        # Count stops
        stops_data = execute_query("SELECT COUNT(*) as count FROM stops")
        stats['stops'] = stops_data[0]['count'] if stops_data else 0
        
        # Count routes
        routes_data = execute_query("SELECT COUNT(*) as count FROM routes")
        stats['routes'] = routes_data[0]['count'] if routes_data else 0
        
        # Count trips
        trips_data = execute_query("SELECT COUNT(*) as count FROM trips")
        stats['trips'] = trips_data[0]['count'] if trips_data else 0
        
        # Count stop_times
        stop_times_data = execute_query("SELECT COUNT(*) as count FROM stop_times")
        stats['stop_times'] = stop_times_data[0]['count'] if stop_times_data else 0
        
        # Get last update times (if available)
        try:
            last_update_data = execute_query("""
                SELECT 
                    MAX(CASE WHEN table_name = 'stops' THEN updated_at END) as stops_updated,
                    MAX(CASE WHEN table_name = 'routes' THEN updated_at END) as routes_updated,
                    MAX(CASE WHEN table_name = 'trips' THEN updated_at END) as trips_updated
                FROM (
                    SELECT 'stops' as table_name, NOW() as updated_at
                    UNION SELECT 'routes', NOW()  
                    UNION SELECT 'trips', NOW()
                ) t
            """)
        except:
            # If update tracking doesn't exist, use current time
            pass
        
        return jsonify({
            "database_stats": stats,
            "data_sources": {
                "stops": "AT GTFS API (12h intervals)",
                "routes": "AT GTFS API (12h intervals)",
                "trips": "GTFS Static (24h intervals)", 
                "stop_times": "GTFS Static (24h intervals)"
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stats: {e}")
        return jsonify({"error": "Failed to get statistics"}), 500

# STOPS ENDPOINTS
@app.route('/api/stops')
def get_stops():
    """Get all stops with optional filtering and pagination"""
    try:
        # Pagination parameters
        page = request.args.get('page', 1, type=int)
        per_page = min(request.args.get('per_page', 50, type=int), 500)
        offset = (page - 1) * per_page
        
        # Search parameters
        search = request.args.get('search', '').strip()
        location_type = request.args.get('location_type', type=int)
        
        # Build query
        query = """
            SELECT stop_id, stop_code, stop_name, stop_lat, stop_lon, 
                   location_type, wheelchair_boarding
            FROM stops
        """
        params = []
        conditions = []
        
        if search:
            conditions.append("(stop_name ILIKE %s OR stop_code ILIKE %s)")
            search_param = f"%{search}%"
            params.extend([search_param, search_param])
        
        if location_type is not None:
            conditions.append("location_type = %s")
            params.append(location_type)
        
        if conditions:
            query += " WHERE " + " AND ".join(conditions)
        
        # Count total for pagination
        count_query = query.replace("SELECT stop_id, stop_code, stop_name, stop_lat, stop_lon, location_type, wheelchair_boarding", "SELECT COUNT(*)")
        count_data = execute_query(count_query, params)
        total = count_data[0]['count'] if count_data else 0
        
        # Add ordering and pagination
        query += " ORDER BY stop_name LIMIT %s OFFSET %s"
        params.extend([per_page, offset])
        
        data = execute_query(query, params)
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        return jsonify({
            "stops": data,
            "pagination": {
                "page": page,
                "per_page": per_page,
                "total": total,
                "pages": (total + per_page - 1) // per_page
            },
            "filters": {
                "search": search if search else None,
                "location_type": location_type
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stops: {e}")
        return jsonify({"error": "Internal server error"}), 500

@app.route('/api/stops/<stop_id>')
def get_stop(stop_id):
    """Get specific stop by ID"""
    try:
        query = """
            SELECT stop_id, stop_code, stop_name, stop_lat, stop_lon, 
                   location_type, wheelchair_boarding
            FROM stops 
            WHERE stop_id = %s
        """
        
        data = execute_query(query, [stop_id])
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        if not data:
            return jsonify({"error": "Stop not found"}), 404
        
        return jsonify({
            "stop": data[0],
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stop: {e}")
        return jsonify({"error": "Internal server error"}), 500

# ROUTES ENDPOINTS
@app.route('/api/routes')
def get_routes():
    """Get all routes with optional filtering and pagination"""
    try:
        # Pagination parameters
        page = request.args.get('page', 1, type=int)
        per_page = min(request.args.get('per_page', 50, type=int), 500)
        offset = (page - 1) * per_page
        
        # Search parameters
        search = request.args.get('search', '').strip()
        route_type = request.args.get('route_type', type=int)
        
        # Build query
        query = """
            SELECT route_id, agency_id, route_short_name, route_long_name, route_type
            FROM routes
        """
        params = []
        conditions = []
        
        if search:
            conditions.append("(route_short_name ILIKE %s OR route_long_name ILIKE %s)")
            search_param = f"%{search}%"
            params.extend([search_param, search_param])
        
        if route_type is not None:
            conditions.append("route_type = %s")
            params.append(route_type)
        
        if conditions:
            query += " WHERE " + " AND ".join(conditions)
        
        # Count total for pagination
        count_query = query.replace("SELECT route_id, agency_id, route_short_name, route_long_name, route_type", "SELECT COUNT(*)")
        count_data = execute_query(count_query, params)
        total = count_data[0]['count'] if count_data else 0
        
        # Add ordering and pagination
        query += " ORDER BY route_short_name LIMIT %s OFFSET %s"
        params.extend([per_page, offset])
        
        data = execute_query(query, params)
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        return jsonify({
            "routes": data,
            "pagination": {
                "page": page,
                "per_page": per_page,
                "total": total,
                "pages": (total + per_page - 1) // per_page
            },
            "filters": {
                "search": search if search else None,
                "route_type": route_type
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_routes: {e}")
        return jsonify({"error": "Internal server error"}), 500

@app.route('/api/routes/<route_id>')
def get_route(route_id):
    """Get specific route by ID"""
    try:
        query = """
            SELECT route_id, agency_id, route_short_name, route_long_name, route_type
            FROM routes 
            WHERE route_id = %s
        """
        
        data = execute_query(query, [route_id])
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        if not data:
            return jsonify({"error": "Route not found"}), 404
        
        return jsonify({
            "route": data[0],
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_route: {e}")
        return jsonify({"error": "Internal server error"}), 500

# TRIPS ENDPOINTS
@app.route('/api/trips')
def get_trips():
    """Get trips with filtering and pagination"""
    try:
        # Pagination parameters
        page = request.args.get('page', 1, type=int)
        per_page = min(request.args.get('per_page', 50, type=int), 500)
        offset = (page - 1) * per_page
        
        # Filter parameters
        route_id = request.args.get('route_id')
        service_id = request.args.get('service_id')
        direction_id = request.args.get('direction_id', type=int)
        
        # Build query
        query = """
            SELECT trip_id, route_id, service_id, trip_headsign, direction_id,
                   block_id, shape_id, wheelchair_accessible, bikes_allowed
            FROM trips
        """
        params = []
        conditions = []
        
        if route_id:
            conditions.append("route_id = %s")
            params.append(route_id)
        
        if service_id:
            conditions.append("service_id = %s")
            params.append(service_id)
        
        if direction_id is not None:
            conditions.append("direction_id = %s")
            params.append(direction_id)
        
        if conditions:
            query += " WHERE " + " AND ".join(conditions)
        
        # Count total for pagination
        count_query = query.replace("SELECT trip_id, route_id, service_id, trip_headsign, direction_id, block_id, shape_id, wheelchair_accessible, bikes_allowed", "SELECT COUNT(*)")
        count_data = execute_query(count_query, params)
        total = count_data[0]['count'] if count_data else 0
        
        # Add ordering and pagination
        query += " ORDER BY route_id, trip_id LIMIT %s OFFSET %s"
        params.extend([per_page, offset])
        
        data = execute_query(query, params)
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        return jsonify({
            "trips": data,
            "pagination": {
                "page": page,
                "per_page": per_page,
                "total": total,
                "pages": (total + per_page - 1) // per_page
            },
            "filters": {
                "route_id": route_id,
                "service_id": service_id,
                "direction_id": direction_id
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_trips: {e}")
        return jsonify({"error": "Internal server error"}), 500

@app.route('/api/trips/<trip_id>')
def get_trip(trip_id):
    """Get specific trip by ID"""
    try:
        query = """
            SELECT trip_id, route_id, service_id, trip_headsign, direction_id,
                   block_id, shape_id, wheelchair_accessible, bikes_allowed
            FROM trips 
            WHERE trip_id = %s
        """
        
        data = execute_query(query, [trip_id])
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        if not data:
            return jsonify({"error": "Trip not found"}), 404
        
        return jsonify({
            "trip": data[0],
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_trip: {e}")
        return jsonify({"error": "Internal server error"}), 500

# STOP TIMES ENDPOINTS
@app.route('/api/stop_times')
def get_stop_times():
    """Get stop times with required filtering (large dataset protection)"""
    try:
        # Require at least one filter to prevent massive responses
        trip_id = request.args.get('trip_id')
        stop_id = request.args.get('stop_id')
        
        if not trip_id and not stop_id:
            return jsonify({
                "error": "Filter required",
                "message": "Please provide either trip_id or stop_id parameter to filter results",
                "example_usage": [
                    "/api/stop_times?trip_id=12345",
                    "/api/stop_times?stop_id=1001",
                    "/api/stop_times?trip_id=12345&stop_id=1001"
                ]
            }), 400
        
        # Pagination parameters
        page = request.args.get('page', 1, type=int)
        per_page = min(request.args.get('per_page', 100, type=int), 1000)
        offset = (page - 1) * per_page
        
        # Build query
        query = """
            SELECT trip_id, arrival_time, departure_time, stop_id, stop_sequence,
                   pickup_type, drop_off_type, shape_dist_traveled
            FROM stop_times
        """
        params = []
        conditions = []
        
        if trip_id:
            conditions.append("trip_id = %s")
            params.append(trip_id)
        
        if stop_id:
            conditions.append("stop_id = %s")
            params.append(stop_id)
        
        query += " WHERE " + " AND ".join(conditions)
        
        # Count total for pagination
        count_query = query.replace("SELECT trip_id, arrival_time, departure_time, stop_id, stop_sequence, pickup_type, drop_off_type, shape_dist_traveled", "SELECT COUNT(*)")
        count_data = execute_query(count_query, params)
        total = count_data[0]['count'] if count_data else 0
        
        # Add ordering and pagination
        query += " ORDER BY trip_id, stop_sequence LIMIT %s OFFSET %s"
        params.extend([per_page, offset])
        
        data = execute_query(query, params)
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        return jsonify({
            "stop_times": data,
            "pagination": {
                "page": page,
                "per_page": per_page,
                "total": total,
                "pages": (total + per_page - 1) // per_page
            },
            "filters": {
                "trip_id": trip_id,
                "stop_id": stop_id
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stop_times: {e}")
        return jsonify({"error": "Internal server error"}), 500

@app.route('/api/stop_times/<stop_id>')
def get_stop_times_for_stop(stop_id):
    """Get upcoming stop times for a specific stop with route information"""
    try:
        # Optional time filtering
        current_time = request.args.get('time')  # Format: HH:MM:SS
        limit = min(request.args.get('limit', 20, type=int), 100)
        
        # Build query with joins for route information
        query = """
            SELECT st.trip_id, st.arrival_time, st.departure_time, st.stop_sequence,
                   t.route_id, t.trip_headsign, t.direction_id,
                   r.route_short_name, r.route_long_name
            FROM stop_times st
            JOIN trips t ON st.trip_id = t.trip_id
            JOIN routes r ON t.route_id = r.route_id
            WHERE st.stop_id = %s
        """
        params = [stop_id]
        
        if current_time:
            query += " AND st.departure_time >= %s"
            params.append(current_time)
        
        query += " ORDER BY st.departure_time LIMIT %s"
        params.append(limit)
        
        data = execute_query(query, params)
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        return jsonify({
            "stop_id": stop_id,
            "stop_times": data,
            "filters": {
                "current_time": current_time,
                "limit": limit
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stop_times_for_stop: {e}")
        return jsonify({"error": "Internal server error"}), 500

# NEW ENDPOINT: Get trips for a specific stop
@app.route('/api/stops/<stop_id>/trips')
def get_trips_for_stop(stop_id):
    """Get all trips that serve a specific stop"""
    try:
        # Optional filtering
        route_id = request.args.get('route_id')
        direction_id = request.args.get('direction_id', type=int)
        limit = min(request.args.get('limit', 50, type=int), 200)
        
        # Build query
        query = """
            SELECT DISTINCT t.trip_id, t.route_id, t.service_id, t.trip_headsign, 
                   t.direction_id, r.route_short_name, r.route_long_name,
                   st.departure_time, st.stop_sequence
            FROM trips t
            JOIN stop_times st ON t.trip_id = st.trip_id
            JOIN routes r ON t.route_id = r.route_id
            WHERE st.stop_id = %s
        """
        params = [stop_id]
        
        if route_id:
            query += " AND t.route_id = %s"
            params.append(route_id)
        
        if direction_id is not None:
            query += " AND t.direction_id = %s"
            params.append(direction_id)
        
        query += " ORDER BY st.departure_time LIMIT %s"
        params.append(limit)
        
        data = execute_query(query, params)
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        return jsonify({
            "stop_id": stop_id,
            "trips": data,
            "count": len(data),
            "filters": {
                "route_id": route_id,
                "direction_id": direction_id,
                "limit": limit
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_trips_for_stop: {e}")
        return jsonify({"error": "Internal server error"}), 500

if __name__ == '__main__':
    log("Starting Auckland Transport GTFS REST API Server v2.0")
    log("Data sources: AT GTFS API (stops/routes) + GTFS Static (trips/stop_times)")
    app.run(host='0.0.0.0', port=5000, debug=False)
