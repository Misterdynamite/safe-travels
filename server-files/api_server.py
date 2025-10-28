from flask import Flask, jsonify, request, g
import os
import logging
import uuid
import time
import psycopg2
from datetime import datetime
from zoneinfo import ZoneInfo
from typing import Optional, Tuple, Dict, Any, List
from concurrent.futures import ThreadPoolExecutor, as_completed
from realtime_integration import ATRealtimeIntegration

app = Flask(__name__)
logging.getLogger('werkzeug').disabled = True
app.logger.disabled = True

# Logging setup
logger = logging.getLogger("at_api_server")
handler = logging.StreamHandler()
handler.setFormatter(logging.Formatter('[%(asctime)s] %(levelname)-8s %(message)s', '%H:%M:%S'))
logger.handlers = [handler]
logger.setLevel(os.environ.get("LOG_LEVEL", "INFO").upper())
logger.propagate = False

# Database config
DB_CONFIG = {
    "dbname": os.environ.get("AT_DB_NAME", "safetravels"),
    "user": os.environ.get("AT_DB_USER", "remote"),
    "password": os.environ.get("AT_DB_PASSWORD", "safetravels"),
    "host": os.environ.get("AT_DB_HOST", "localhost"),
    "port": os.environ.get("AT_DB_PORT", "5432")
}

# Real-time integration
rt_integration = ATRealtimeIntegration(os.environ.get("AT_API_KEY", "25c926c6234a49c98d52d90a8bd7ac7e"))

# Thread pool for async realtime fetching (reuse across requests)
_rt_executor = ThreadPoolExecutor(max_workers=10, thread_name_prefix="RT-Worker")

def log(message: str, level: str = "info", **context):
    """Structured logging."""
    if context:
        ctx = " ".join(f"{k}={v}" for k, v in context.items())
        message = f"{message} [{ctx}]"
    getattr(logger, level.lower(), logger.info)(message)

@app.before_request
def _log_request_start():
    g.request_id = request.headers.get("X-Request-Id", uuid.uuid4().hex[:8])
    request._log_start_time = time.time()
    client_ip = (request.headers.get('X-Forwarded-For', request.remote_addr) or '').split(',')[0].strip()
    params = f"?{'&'.join(f'{k}={v}' for k, v in request.args.items())}" if request.args else ""
    print("START", flush=True)
    log(f"{client_ip} - {request.method} {request.path}{params}")

@app.after_request
def _log_request_end(response):
    duration_ms = round((time.time() - getattr(request, "_log_start_time", time.time())) * 1000, 2)
    status = response.status_code
    
    # Skip noise
    if status == 404 and any(request.path.startswith(p) for p in ['/favicon.ico', '/robots.txt', '/.well-known/', '/wp-admin/', '/admin/']):
        response.headers["X-Request-Id"] = getattr(g, "request_id", "")
        return response
    
    status_msg = {2: "SUCCESS", 3: "REDIRECT", 4: "CLIENT ERROR", 5: "SERVER ERROR"}.get(status // 100, "UNKNOWN")
    log(f"{status_msg} ({status}) - {duration_ms}ms")
    print("END", flush=True)
    response.headers["X-Request-Id"] = getattr(g, "request_id", "")
    return response

def get_db_connection():
    return psycopg2.connect(**DB_CONFIG)

def execute_query(query, params=None):
    """Execute database query with error handling."""
    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute(query, params or [])
                if query.strip().upper().startswith('SELECT'):
                    columns = [desc[0] for desc in cur.description]
                    return [dict(zip(columns, row)) for row in cur.fetchall()]
                conn.commit()
                return []
    except Exception as e:
        log(f"Database error: {e}", level="error")
        return None

SECONDS_IN_DAY = 86400

def _parse_gtfs_time(time_str: Optional[str]) -> Optional[int]:
    """Convert GTFS time to seconds since midnight."""
    if not time_str:
        return None
    try:
        h, m, s = map(int, time_str.split(":"))
        return h * 3600 + m * 60 + s
    except ValueError:
        return None

def _compute_trip_schedule(trip_id: str) -> Tuple[Optional[int], Optional[int], Dict[str, Any]]:
    """Get trip schedule window."""
    data = execute_query("""
        SELECT MIN(arrival_time) AS first_arrival, MIN(departure_time) AS first_departure,
               MAX(arrival_time) AS last_arrival, MAX(departure_time) AS last_departure
        FROM stop_times WHERE trip_id = %s
    """, [trip_id])
    
    if not data:
        return None, None, {}
    
    row = data[0]
    times = [row.get("first_departure"), row.get("first_arrival"), 
             row.get("last_departure"), row.get("last_arrival")]
    parsed = [_parse_gtfs_time(t) for t in times]
    
    earliest = min((t for t in parsed[:2] if t is not None), default=None)
    latest = max((t for t in parsed[2:] if t is not None), default=None)
    
    return earliest, latest, {k: row.get(k) for k in ["first_departure", "first_arrival", "last_departure", "last_arrival"]}

def _trip_requires_realtime(trip_id: str) -> Tuple[bool, Dict[str, Any]]:
    """Check if trip is currently active and needs real-time data."""
    earliest, latest, info = _compute_trip_schedule(trip_id)
    
    if earliest is None or latest is None:
        info["reason"] = "insufficient_schedule_data"
        return False, info
    
    now = datetime.now()
    current_secs = now.hour * 3600 + now.minute * 60 + now.second
    adjusted = current_secs + SECONDS_IN_DAY if latest >= SECONDS_IN_DAY and current_secs < earliest else current_secs
    
    is_active = earliest <= adjusted <= latest
    info.update({
        "earliest_seconds": earliest, "latest_seconds": latest,
        "current_seconds": current_secs, "adjusted_current_seconds": adjusted,
        "evaluated_at": now.isoformat(), "reason": "active_window" if is_active else "outside_window"
    })
    
    return is_active, info

@app.route('/')
def root():
    """API information."""
    return jsonify({
        "message": "Auckland Transport GTFS REST API with Real-time Enhancement",
        "version": "3.0",
        "endpoints": {
            "stops": "/api/stops, /api/stops/{id}, /api/stops/{id}/trips, /api/stops/{id}/alerts",
            "routes": "/api/routes/{id}/alerts",
            "trips": "/api/trips/{id}",
            "stop_times": "/api/stop_times/{stop_id}",
            "realtime": "/api/realtime/trip-updates, /api/realtime/vehicle-positions, /api/realtime/service-alerts",
            "stats": "/api/stats"
        },
        "timestamp": datetime.now().isoformat()
    })

# Simple error handlers
@app.errorhandler(404)
def not_found(e):
    return jsonify({"error": "Not found"}), 404

@app.errorhandler(400)
def bad_request(e):
    return jsonify({"error": "Bad request"}), 400

@app.errorhandler(500)
def internal_error(e):
    return jsonify({"error": "Internal server error"}), 500
@app.route('/api/stats')
def get_stats():
    """Get database and real-time statistics."""
    try:
        stats = {'database': {}, 'realtime': {}}
        
        # Get counts
        for table in ['stops', 'routes', 'trips', 'stop_times']:
            data = execute_query(f"SELECT COUNT(*) as count FROM {table}")
            stats['database'][table] = data[0]['count'] if data else 0
        
        # Real-time stats
        try:
            trip_updates = rt_integration.get_trip_updates()
            vehicle_positions = rt_integration.get_vehicle_positions()
            service_alerts = rt_integration.get_service_alerts()
            
            stats['realtime'] = {
                'trip_updates': len(trip_updates),
                'vehicle_positions': len(vehicle_positions),
                'service_alerts': len(service_alerts),
                'last_updated': datetime.now().isoformat(),
                'status': 'operational'
            }
        except Exception as e:
            log(f"Error getting real-time stats: {e}", level="error")
            stats['realtime'] = {'status': 'unavailable', 'error': str(e)}
        
        log(f"[STATS] DB: {stats['database']['stops']} stops, {stats['database']['routes']} routes | "
            f"RT: {stats['realtime'].get('trip_updates', 0)} updates, {stats['realtime'].get('vehicle_positions', 0)} vehicles")
        
        return jsonify({
            "stats": stats,
            "data_sources": {
                "stops": "AT GTFS API (12h) + Real-time alerts",
                "routes": "AT GTFS API (12h) + Real-time alerts",
                "trips": "GTFS Static (24h) + Real-time updates",
                "stop_times": "GTFS Static (24h) + Real-time delays"
            },
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stats: {e}", level="error")
        return jsonify({"error": "Failed to get statistics"}), 500

# Enhanced Stop Times endpoint with automatic real-time data injection
@app.route('/api/stop_times/<stop_id>')
def get_stop_times_realtime(stop_id):
    """Get stop times with automatic real-time delay information."""
    try:
        nz_tz = ZoneInfo("Pacific/Auckland")
        now = datetime.now(nz_tz)
        current_time = request.args.get('time', now.strftime('%H:%M:%S'))
        limit = min(request.args.get('limit', 20, type=int), 100)
        
        static_data = execute_query("""
            SELECT st.trip_id, st.arrival_time, st.departure_time, st.stop_sequence,
                   t.route_id, t.trip_headsign, t.direction_id, t.service_id,
                   r.route_short_name, r.route_long_name,
                   (SELECT MIN(st2.departure_time) FROM stop_times st2 
                    WHERE st2.trip_id = t.trip_id) as trip_start_time
            FROM stop_times st
            JOIN trips t ON st.trip_id = t.trip_id
            JOIN routes r ON t.route_id = r.route_id
            WHERE st.stop_id = %s AND st.departure_time >= %s
            ORDER BY st.departure_time LIMIT %s
        """, [stop_id, current_time, limit])
        
        if static_data is None:
            return jsonify({"error": "Database error"}), 500
        
        # Filter by service patterns
        valid_patterns = _get_valid_service_patterns(now.strftime('%A'))
        static_data = [s for s in static_data if any(p in s.get('service_id', '') for p in valid_patterns)]
        
        # Check if any trips are active - if so, fetch fresh realtime
        current_secs = now.hour * 3600 + now.minute * 60 + now.second
        has_active_trips = any(
            _parse_gtfs_time(s.get('trip_start_time')) is not None and 
            _parse_gtfs_time(s.get('trip_start_time')) <= current_secs 
            for s in static_data
        )
        
        # Enhance with realtime (force fresh if any trips are active)
        enhanced_data = rt_integration.enhance_stop_times_with_realtime(
            static_data, 
            stop_id, 
            force_fresh=has_active_trips
        )
        enhanced_count = sum(1 for item in enhanced_data if item.get("is_realtime", False))
        
        # Get alerts
        alerts = rt_integration.get_alerts_for_stop(stop_id)
        
        stop_info = execute_query("SELECT stop_name FROM stops WHERE stop_id = %s", [stop_id])
        stop_name = stop_info[0]['stop_name'] if stop_info else stop_id
        
        data_source = "LIVE" if has_active_trips else "CACHED"
        log(f"[STOP] '{stop_name}' - {len(enhanced_data)} times ({enhanced_count} realtime, {data_source}) | {len(alerts)} alerts")
        
        return jsonify({"status": "OK", "response": enhanced_data, "error": None})
        
    except Exception as e:
        log(f"Error in get_stop_times_realtime: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

# Enhanced Trip endpoint with automatic real-time data injection
@app.route('/api/trips/<trip_id>')
def get_trip_realtime(trip_id):
    """Get trip with automatic real-time information."""
    try:
        static_data = execute_query("""
            SELECT trip_id, route_id, service_id, trip_headsign, direction_id,
                   block_id, shape_id, wheelchair_accessible, bikes_allowed
            FROM trips WHERE trip_id = %s
        """, [trip_id])
        
        if static_data is None:
            return jsonify({"error": "Database error"}), 500
        if not static_data:
            return jsonify({"error": "Trip not found"}), 404
        
        # Check if realtime refresh needed
        try:
            force_refresh, _ = _trip_requires_realtime(trip_id)
        except Exception:
            force_refresh = False
        
        enhanced_trip = rt_integration.get_trip_with_realtime(trip_id, static_data[0], force_refresh=force_refresh)
        route_alerts = rt_integration.get_alerts_for_route(enhanced_trip["route_id"])
        
        trip_name = enhanced_trip.get("trip_headsign", trip_id)
        route_name = enhanced_trip.get("route_id", "Unknown")
        is_realtime = enhanced_trip.get("is_realtime", False)
        
        if force_refresh:
            if is_realtime:
                delay = enhanced_trip.get('delay_seconds', 0)
                delay_str = f"+{delay}s" if delay > 0 else (f"{abs(delay)}s early" if delay < 0 else "on time")
                log(f"[TRIP] '{trip_name}' ({route_name}) - LIVE RT {delay_str} | {len(route_alerts)} alerts")
            else:
                log(f"[TRIP] '{trip_name}' ({route_name}) - Active but no RT data | {len(route_alerts)} alerts")
        else:
            log(f"[TRIP] '{trip_name}' ({route_name}) - Cached (inactive) | {len(route_alerts)} alerts")
        
        return jsonify({"status": "OK", "response": enhanced_trip, "error": None})
        
    except Exception as e:
        log(f"Error in get_trip_realtime: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

# Enhanced Trip Stops endpoint with automatic real-time data injection
@app.route('/api/trips/<trip_id>/stops')
def get_trip_stops_realtime(trip_id):
    """Get all remaining stops for a trip with automatic real-time delay information."""
    try:
        nz_tz = ZoneInfo("Pacific/Auckland")
        now = datetime.now(nz_tz)
        current_time_seconds = now.hour * 3600 + now.minute * 60 + now.second
        
        # Get all stop_times for this trip with stop details
        static_data = execute_query("""
            SELECT st.trip_id, st.stop_id, st.arrival_time, st.departure_time, 
                   st.stop_sequence, st.pickup_type, st.drop_off_type,
                   s.stop_name, s.stop_lat, s.stop_lon, s.location_type,
                   t.route_id, t.trip_headsign, t.service_id
            FROM stop_times st
            JOIN stops s ON st.stop_id = s.stop_id
            JOIN trips t ON st.trip_id = t.trip_id
            WHERE st.trip_id = %s
            ORDER BY st.stop_sequence
        """, [trip_id])
        
        if static_data is None:
            return jsonify({"error": "Database error"}), 500
        if not static_data:
            return jsonify({"error": "Trip not found or has no stops"}), 404
        
        # Filter to only future stops
        future_stops = []
        for stop in static_data:
            departure_seconds = _parse_gtfs_time(stop.get('departure_time'))
            if departure_seconds is not None and departure_seconds >= current_time_seconds:
                future_stops.append(stop)
        
        if not future_stops:
            return jsonify({
                "trip_id": trip_id,
                "stops": [],
                "has_realtime": False,
                "message": "All stops have been passed"
            })
        
        # Check if realtime refresh needed
        try:
            force_refresh, _ = _trip_requires_realtime(trip_id)
        except Exception:
            force_refresh = False
        
        trip_name = static_data[0].get("trip_headsign", trip_id) if static_data else trip_id
        route_name = static_data[0].get("route_id", "Unknown") if static_data else "Unknown"
        
        # Get realtime update for this trip (single call)
        trip_update = None
        if force_refresh:
            trip_update = rt_integration.get_trip_update_by_id(trip_id, use_cache_fallback=True)
        else:
            # Use cached data
            trip_update = rt_integration.get_trip_update_by_id(trip_id, use_cache_fallback=True)
        
        # Apply realtime delays to all future stops
        enhanced_stops = []
        has_realtime = False
        
        if trip_update and 'stop_time_updates' in trip_update:
            # stop_time_updates is already a dict mapping stop_id to update data
            stop_updates_map = trip_update['stop_time_updates']
            has_realtime = bool(stop_updates_map)
            
            for stop in future_stops:
                stop_copy = dict(stop)
                stop_id = stop['stop_id']
                
                # Check if we have realtime data for this specific stop
                if stop_id in stop_updates_map:
                    stu = stop_updates_map[stop_id]
                    delay_seconds = stu.get('arrival', {}).get('delay', 0)
                    
                    # Apply delay to times
                    if stop['arrival_time']:
                        stop_copy['arrival_time'] = _apply_delay_to_time(stop['arrival_time'], delay_seconds)
                    if stop['departure_time']:
                        stop_copy['departure_time'] = _apply_delay_to_time(stop['departure_time'], delay_seconds)
                    
                    stop_copy['is_realtime'] = True
                    stop_copy['delay_seconds'] = delay_seconds
                else:
                    # No specific update for this stop, use scheduled times
                    stop_copy['is_realtime'] = False
                    stop_copy['delay_seconds'] = 0
                
                enhanced_stops.append(stop_copy)
        else:
            # No realtime data available, return scheduled times
            for stop in future_stops:
                stop_copy = dict(stop)
                stop_copy['is_realtime'] = False
                stop_copy['delay_seconds'] = 0
                enhanced_stops.append(stop_copy)
        
        if force_refresh:
            if has_realtime:
                log(f"[TRIP STOPS] '{trip_name}' ({route_name}) - {len(enhanced_stops)} remaining stops with LIVE RT data")
            else:
                log(f"[TRIP STOPS] '{trip_name}' ({route_name}) - {len(enhanced_stops)} remaining stops, active but no RT data")
        else:
            log(f"[TRIP STOPS] '{trip_name}' ({route_name}) - {len(enhanced_stops)} remaining stops using cache")
        
        return jsonify({
            "trip_id": trip_id,
            "stops": enhanced_stops,
            "has_realtime": has_realtime
        })
    except Exception as e:
        log(f"Error in get_trip_stops_realtime: {e}", level="error")
        return jsonify({"error": str(e)}), 500

@app.route('/api/stops/<stop_id>/alerts')
def get_stop_alerts(stop_id):
    """Get service alerts for a specific stop."""
    try:
        stop_data = execute_query("SELECT stop_id, stop_name FROM stops WHERE stop_id = %s", [stop_id])
        if not stop_data:
            return jsonify({"error": "Stop not found"}), 404
        
        alerts = rt_integration.get_alerts_for_stop(stop_id)
        stop_name = stop_data[0].get('stop_name', stop_id)
        
        log(f"[ALERTS] STOP '{stop_name}' - {len(alerts)} alerts")
        
        return jsonify({
            "stop": stop_data[0],
            "alerts": alerts,
            "alert_count": len(alerts),
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stop_alerts: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

@app.route('/api/routes/<route_id>/alerts')
def get_route_alerts(route_id):
    """Get service alerts for a specific route."""
    try:
        route_data = execute_query("""
            SELECT route_id, route_short_name, route_long_name 
            FROM routes WHERE route_id = %s
        """, [route_id])
        
        if not route_data:
            return jsonify({"error": "Route not found"}), 404
        
        alerts = rt_integration.get_alerts_for_route(route_id)
        route_name = route_data[0].get('route_short_name') or route_data[0].get('route_long_name', route_id)
        
        log(f"[ALERTS] ROUTE '{route_name}' - {len(alerts)} alerts")
        
        return jsonify({
            "route": route_data[0],
            "alerts": alerts,
            "alert_count": len(alerts),
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_route_alerts: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

# Direct Real-time API endpoints
@app.route('/api/realtime/trip-updates')
def get_realtime_trip_updates():
    """Get all real-time trip updates."""
    try:
        trip_updates = rt_integration.get_trip_updates()
        trip_id = request.args.get('trip_id')
        
        if trip_id:
            trip_updates = {k: v for k, v in trip_updates.items() if k == trip_id}
        
        log(f"[REALTIME] TRIP UPDATES - {len(trip_updates)} returned")
        
        return jsonify({
            "trip_updates": trip_updates,
            "count": len(trip_updates),
            "timestamp": datetime.now().isoformat()
        })
    except Exception as e:
        log(f"Error in get_realtime_trip_updates: {e}", level="error")
        return jsonify({"error": "Failed to get trip updates"}), 500

@app.route('/api/realtime/vehicle-positions')
def get_realtime_vehicle_positions():
    """Get all real-time vehicle positions."""
    try:
        positions = rt_integration.get_vehicle_positions()
        route_id = request.args.get('route_id')
        trip_id = request.args.get('trip_id')
        
        if route_id:
            positions = {k: v for k, v in positions.items() if v.get("route_id") == route_id}
        if trip_id:
            positions = {k: v for k, v in positions.items() if v.get("trip_id") == trip_id}
        
        log(f"[REALTIME] VEHICLE POSITIONS - {len(positions)} returned")
        
        return jsonify({
            "vehicle_positions": positions,
            "count": len(positions),
            "timestamp": datetime.now().isoformat()
        })
    except Exception as e:
        log(f"Error in get_realtime_vehicle_positions: {e}", level="error")
        return jsonify({"error": "Failed to get vehicle positions"}), 500

@app.route('/api/realtime/service-alerts')
def get_realtime_service_alerts():
    """Get all real-time service alerts."""
    try:
        alerts = rt_integration.get_service_alerts()
        severity = request.args.get('severity')
        
        if severity:
            alerts = [a for a in alerts if a.get("severity_level") == severity]
        
        log(f"[REALTIME] SERVICE ALERTS - {len(alerts)} returned")
        
        return jsonify({
            "service_alerts": alerts,
            "count": len(alerts),
            "timestamp": datetime.now().isoformat()
        })
    except Exception as e:
        log(f"Error in get_realtime_service_alerts: {e}", level="error")
        return jsonify({"error": "Failed to get service alerts"}), 500

# Keep all existing endpoints from the original api_server_v2.py
# (I'll include the essential ones here for completeness)

@app.route('/api/stops')
def get_stops():
    """Get all stops with optional filtering and pagination."""
    try:
        page = request.args.get('page', 1, type=int)
        per_page = min(request.args.get('per_page', 50, type=int), 500)
        offset = (page - 1) * per_page
        search = request.args.get('search', '').strip()
        location_type = request.args.get('location_type', type=int)
        include_alerts = request.args.get('include_alerts', 'false').lower() == 'true'
        
        # Build query
        conditions = []
        params = []
        
        if search:
            conditions.append("(stop_name ILIKE %s OR stop_code ILIKE %s)")
            params.extend([f"%{search}%", f"%{search}%"])
        if location_type is not None:
            conditions.append("location_type = %s")
            params.append(location_type)
        
        where_clause = f" WHERE {' AND '.join(conditions)}" if conditions else ""
        
        # Get total count
        count_data = execute_query(f"SELECT COUNT(*) as count FROM stops{where_clause}", params)
        total = count_data[0]['count'] if count_data else 0
        
        # Get stops
        data = execute_query(f"""
            SELECT stop_id, stop_code, stop_name, stop_lat, stop_lon, 
                   location_type, wheelchair_boarding
            FROM stops{where_clause}
            ORDER BY stop_name LIMIT %s OFFSET %s
        """, params + [per_page, offset])
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        
        # Add alert counts if requested
        if include_alerts:
            try:
                for stop in data:
                    alerts = rt_integration.get_alerts_for_stop(stop["stop_id"])
                    stop["alert_count"] = len(alerts)
            except Exception as e:
                log(f"Error getting alert counts: {e}", level="error")
        
        log(f"Stops list - page {page}, {len(data)} returned, {total} total")
        
        return jsonify({
            "stops": data,
            "pagination": {
                "page": page,
                "per_page": per_page,
                "total": total,
                "pages": (total + per_page - 1) // per_page
            },
            "filters": {"search": search, "location_type": location_type, "include_alerts": include_alerts},
            "timestamp": datetime.now().isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stops: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

def _get_valid_service_patterns(day_of_week: str) -> list:
    """Get valid service patterns for a day."""
    patterns = ['Daily']
    if day_of_week in ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']:
        patterns.extend(['Weekday', 'ExSat', 'ExSun'])
        if day_of_week == 'Friday':
            patterns.append('Friday')
    elif day_of_week == 'Saturday':
        patterns.extend(['Weekend', 'Saturday', 'ExSun'])
    elif day_of_week == 'Sunday':
        patterns.extend(['Weekend', 'Sunday', 'ExSat'])
    return patterns

def _apply_delay_to_time(time_str: str, delay: int) -> str:
    """Apply delay in seconds to a time string."""
    if not time_str:
        return time_str
    h, m, s = map(int, time_str.split(':'))
    total = h * 3600 + m * 60 + s + delay
    return f"{total // 3600:02d}:{(total % 3600) // 60:02d}:{total % 60:02d}"

def _fetch_realtime_updates_parallel(trip_ids: List[str], timeout: float = 5.0) -> Dict[str, Any]:
    """
    Fetch realtime updates for multiple trips in parallel.
    
    Args:
        trip_ids: List of trip IDs to fetch
        timeout: Max time to wait for all requests (default 5s)
        
    Returns:
        Dict mapping trip_id to realtime update data
    """
    if not trip_ids:
        return {}
    
    results = {}
    start_time = time.time()
    
    # Submit all tasks
    future_to_trip = {
        _rt_executor.submit(rt_integration.get_trip_update_by_id, trip_id): trip_id 
        for trip_id in trip_ids
    }
    
    # Collect results with timeout
    for future in as_completed(future_to_trip, timeout=timeout):
        trip_id = future_to_trip[future]
        try:
            result = future.result(timeout=1.0)  # Individual result timeout
            if result:
                results[trip_id] = result
        except Exception as e:
            log(f"Error fetching realtime for {trip_id}: {e}", level="warning")
    
    elapsed = time.time() - start_time
    log(f"Fetched {len(results)}/{len(trip_ids)} realtime updates in {elapsed:.2f}s")
    
    return results

@app.route('/api/stops/<stop_id>/trips')
def get_stop_trips(stop_id):
    """Get all trips serving a specific stop."""
    try:
        stop_data = execute_query("SELECT stop_id, stop_name FROM stops WHERE stop_id = %s", [stop_id])
        if not stop_data:
            return jsonify({"error": "Stop not found"}), 404
        
        nz_tz = ZoneInfo("Pacific/Auckland")
        now = datetime.now(nz_tz)
        current_time = request.args.get('time', now.strftime('%H:%M:%S'))
        limit = min(request.args.get('limit', 100, type=int), 500)
        
        trips_data = execute_query("""
            SELECT DISTINCT t.trip_id, t.route_id, t.service_id, t.trip_headsign, 
                   t.direction_id, t.wheelchair_accessible, t.bikes_allowed,
                   r.route_short_name, r.route_long_name, r.route_type,
                   st.arrival_time, st.departure_time, st.stop_sequence,
                   (SELECT MIN(st2.departure_time) FROM stop_times st2 
                    WHERE st2.trip_id = t.trip_id) as trip_start_time
            FROM stop_times st
            JOIN trips t ON st.trip_id = t.trip_id
            JOIN routes r ON t.route_id = r.route_id
            WHERE st.stop_id = %s AND st.departure_time >= %s
            ORDER BY st.departure_time, r.route_short_name LIMIT %s
        """, [stop_id, current_time, limit])
        
        if trips_data is None:
            return jsonify({"error": "Database error"}), 500
        
        # Filter by service patterns
        valid_patterns = _get_valid_service_patterns(now.strftime('%A'))
        trips_data = [t for t in trips_data if any(p in t.get('service_id', '') for p in valid_patterns)]
        
        # Identify active trips that need realtime data
        current_secs = now.hour * 3600 + now.minute * 60 + now.second
        active_trip_ids = []
        trip_index = {}
        
        for idx, trip in enumerate(trips_data):
            trip['service_date'] = now.strftime('%Y%m%d')
            trip_start = _parse_gtfs_time(trip.get('trip_start_time'))
            
            if trip_start and trip_start <= current_secs:
                active_trip_ids.append(trip['trip_id'])
                trip_index[trip['trip_id']] = idx
                trip['is_realtime'] = False  # Will be updated if realtime data is available
            else:
                trip['is_realtime'] = False  # Trip not yet started, using schedule
        
        # Fetch all realtime updates in parallel
        rt_updates = _fetch_realtime_updates_parallel(active_trip_ids) if active_trip_ids else {}
        
        # Apply realtime data to trips
        for trip_id, rt_update in rt_updates.items():
            idx = trip_index[trip_id]
            trip = trips_data[idx]
            delay = rt_update.get("delay", 0)
            
            # Replace scheduled times with real-time predictions
            if delay != 0:
                trip['departure_time'] = _apply_delay_to_time(trip['departure_time'], delay)
                trip['arrival_time'] = _apply_delay_to_time(trip['arrival_time'], delay)
                trip['is_realtime'] = True
                trip['delay_seconds'] = delay
            else:
                trip['is_realtime'] = True
                trip['delay_seconds'] = 0
        
        # Mark trips that didn't get realtime data
        for trip_id in active_trip_ids:
            if trip_id not in rt_updates:
                idx = trip_index[trip_id]
                trips_data[idx]['is_realtime'] = False
        
        stop_name = stop_data[0].get('stop_name', stop_id)
        log(f"[STOP] TRIPS '{stop_name}' - {len(trips_data)} trips ({len(rt_updates)} with realtime)")
        
        return jsonify({
            "stop": stop_data[0],
            "trips": trips_data,
            "count": len(trips_data),
            "limit": limit,
            "realtime_info": {"enhanced_count": len(rt_updates), "total_count": len(trips_data)},
            "timestamp": now.isoformat()
        })
        
    except Exception as e:
        log(f"Error in get_stop_trips: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

@app.route('/api/stops/<stop_id>')
def get_stop(stop_id):
    """Get specific stop by ID with alert information."""
    try:
        data = execute_query("""
            SELECT stop_id, stop_code, stop_name, stop_lat, stop_lon, 
                   location_type, wheelchair_boarding
            FROM stops WHERE stop_id = %s
        """, [stop_id])
        
        if data is None:
            return jsonify({"error": "Database error"}), 500
        if not data:
            return jsonify({"error": "Stop not found"}), 404
        
        stop_data = data[0]
        try:
            alerts = rt_integration.get_alerts_for_stop(stop_id)
            stop_data["current_alerts"] = len(alerts)
            stop_data["has_alerts"] = len(alerts) > 0
        except Exception as e:
            log(f"Error getting alerts for stop {stop_id}: {e}", level="error")
            stop_data["current_alerts"] = 0
            stop_data["has_alerts"] = False
        
        log(f"Stop detail: {stop_id}", has_alerts=stop_data.get("has_alerts"))
        return jsonify({"stop": stop_data, "timestamp": datetime.now().isoformat()})
        
    except Exception as e:
        log(f"Error in get_stop: {e}", level="error")
        return jsonify({"error": "Internal server error"}), 500

if __name__ == '__main__':
    log("Starting Auckland Transport GTFS REST API Server v3.0")
    app.run(host='0.0.0.0', port=5000, debug=False)