import requests
import psycopg2
import time
import sys
import zipfile
import os
import csv
from datetime import datetime

# API key (will add multiple keys in next sprint)
API_KEY = "25c926c6234a49c98d52d90a8bd7ac7e"
GTFS_BASE_URL = "https://api.at.govt.nz/gtfs/v3"
GTFS_STATIC_URL = "https://gtfs.at.govt.nz/gtfs.zip"

# PostgreSQL connection details
DB_CONFIG = {
    "dbname": "safetravels",
    "user": "remote",
    "password": "safetravels",
    "host": "localhost",
    "port": "5432"
}

# Update intervals (in seconds)
GTFS_API_INTERVAL = 43200    # 12 hours for GTFS API data (stops, routes)
GTFS_STATIC_INTERVAL = 86400 # 24 hours for GTFS static data (trips, stop_times, etc.)

# Track last update times
last_gtfs_api_update = 0
last_gtfs_static_update = 0

# Working directory for GTFS files
GTFS_WORK_DIR = "/tmp/gtfs_data"

def log(message):
    """Simple logging with timestamp"""
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{timestamp}] {message}")
    sys.stdout.flush()

def get_db_connection():
    """Get database connection"""
    return psycopg2.connect(**DB_CONFIG)

def ensure_work_dir():
    """Ensure working directory exists"""
    os.makedirs(GTFS_WORK_DIR, exist_ok=True)

def download_gtfs_static():
    """Download and extract GTFS static data"""
    try:
        log("Downloading GTFS static data...")
        ensure_work_dir()
        
        response = requests.get(GTFS_STATIC_URL, timeout=300)
        response.raise_for_status()
        
        zip_path = os.path.join(GTFS_WORK_DIR, "gtfs.zip")
        with open(zip_path, 'wb') as f:
            f.write(response.content)
        
        with zipfile.ZipFile(zip_path, 'r') as zip_ref:
            zip_ref.extractall(GTFS_WORK_DIR)
        
        log(f"GTFS static data downloaded and extracted")
        return True
        
    except Exception as e:
        log(f"ERROR downloading GTFS static data: {e}")
        return False

def fetch_gtfs_api_data(endpoint):
    """Fetch data from AT GTFS API"""
    url = f"{GTFS_BASE_URL}/{endpoint}"
    headers = {"Ocp-Apim-Subscription-Key": API_KEY}
    
    try:
        log(f"Fetching {endpoint} from GTFS API...")
        resp = requests.get(url, headers=headers, timeout=30)
        resp.raise_for_status()
        
        json_data = resp.json()
        if 'data' in json_data:
            data = json_data['data']
            count = len(data) if isinstance(data, list) else 1
            log(f"Fetched {count} {endpoint} records from API")
            return data
        else:
            log(f"WARNING: Unexpected response format for {endpoint}")
            return []
            
    except Exception as e:
        log(f"ERROR fetching {endpoint}: {e}")
        return []

def read_gtfs_csv(filename):
    """Read a GTFS CSV file"""
    try:
        filepath = os.path.join(GTFS_WORK_DIR, filename)
        if not os.path.exists(filepath):
            log(f"GTFS file {filename} not found")
            return []
        
        data = []
        with open(filepath, 'r', encoding='utf-8') as file:
            reader = csv.DictReader(file)
            for row in reader:
                data.append(row)
        
        log(f"Read {len(data)} records from {filename}")
        return data
        
    except Exception as e:
        log(f"ERROR reading {filename}: {e}")
        return []

def process_data_batch(conn, table, data, batch_size=1000):
    """Process data in batches with individual transaction handling"""
    if not data:
        return 0
    
    total_processed = 0
    
    for i in range(0, len(data), batch_size):
        batch = data[i:i + batch_size]
        processed = 0
        
        for item in batch:
            cur = None
            try:
                cur = conn.cursor()
                
                if table == "stops":
                    # Handle both API and CSV formats
                    attr = item.get("attributes", item)
                    cur.execute("""
                        INSERT INTO stops (stop_id, stop_code, stop_name, stop_lat, stop_lon, location_type, wheelchair_boarding)
                        VALUES (%s,%s,%s,%s,%s,%s,%s)
                        ON CONFLICT (stop_id) DO UPDATE
                        SET stop_code=EXCLUDED.stop_code,
                            stop_name=EXCLUDED.stop_name,
                            stop_lat=EXCLUDED.stop_lat,
                            stop_lon=EXCLUDED.stop_lon,
                            location_type=EXCLUDED.location_type,
                            wheelchair_boarding=EXCLUDED.wheelchair_boarding;
                    """, (
                        attr.get("stop_id"), 
                        attr.get("stop_code"), 
                        attr.get("stop_name"),
                        float(attr.get("stop_lat", 0)) if attr.get("stop_lat") else None,
                        float(attr.get("stop_lon", 0)) if attr.get("stop_lon") else None,
                        int(attr.get("location_type", 0)) if attr.get("location_type") else 0,
                        int(attr.get("wheelchair_boarding", 0)) if attr.get("wheelchair_boarding") else 0
                    ))
                
                elif table == "routes":
                    # Manages both API and CSV formats
                    attr = item.get("attributes", item)
                    cur.execute("""
                        INSERT INTO routes (route_id, agency_id, route_short_name, route_long_name, route_type)
                        VALUES (%s,%s,%s,%s,%s)
                        ON CONFLICT (route_id) DO UPDATE
                        SET agency_id=EXCLUDED.agency_id,
                            route_short_name=EXCLUDED.route_short_name,
                            route_long_name=EXCLUDED.route_long_name,
                            route_type=EXCLUDED.route_type;
                    """, (
                        attr.get("route_id"), 
                        attr.get("agency_id"), 
                        attr.get("route_short_name"),
                        attr.get("route_long_name"), 
                        int(attr.get("route_type", 0)) if attr.get("route_type") else 0
                    ))
                
                elif table == "trips":
                    cur.execute("""
                        INSERT INTO trips (trip_id, route_id, service_id, trip_headsign, direction_id, block_id, shape_id, wheelchair_accessible, bikes_allowed)
                        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s)
                        ON CONFLICT (trip_id) DO UPDATE
                        SET route_id=EXCLUDED.route_id,
                            service_id=EXCLUDED.service_id,
                            trip_headsign=EXCLUDED.trip_headsign,
                            direction_id=EXCLUDED.direction_id,
                            block_id=EXCLUDED.block_id,
                            shape_id=EXCLUDED.shape_id,
                            wheelchair_accessible=EXCLUDED.wheelchair_accessible,
                            bikes_allowed=EXCLUDED.bikes_allowed;
                    """, (
                        item.get("trip_id"),
                        item.get("route_id"),
                        item.get("service_id"),
                        item.get("trip_headsign"),
                        int(item.get("direction_id", 0)) if item.get("direction_id") else 0,
                        item.get("block_id"),
                        item.get("shape_id"),
                        int(item.get("wheelchair_accessible", 0)) if item.get("wheelchair_accessible") else 0,
                        int(item.get("bikes_allowed", 0)) if item.get("bikes_allowed") else 0
                    ))
                
                elif table == "stop_times":
                    cur.execute("""
                        INSERT INTO stop_times (trip_id, arrival_time, departure_time, stop_id, stop_sequence, pickup_type, drop_off_type, shape_dist_traveled)
                        VALUES (%s,%s,%s,%s,%s,%s,%s,%s)
                        ON CONFLICT (trip_id, stop_sequence) DO UPDATE
                        SET arrival_time=EXCLUDED.arrival_time,
                            departure_time=EXCLUDED.departure_time,
                            stop_id=EXCLUDED.stop_id,
                            pickup_type=EXCLUDED.pickup_type,
                            drop_off_type=EXCLUDED.drop_off_type,
                            shape_dist_traveled=EXCLUDED.shape_dist_traveled;
                    """, (
                        item.get("trip_id"),
                        item.get("arrival_time"),
                        item.get("departure_time"),
                        item.get("stop_id"),
                        int(item.get("stop_sequence", 0)) if item.get("stop_sequence") else 0,
                        int(item.get("pickup_type", 0)) if item.get("pickup_type") else 0,
                        int(item.get("drop_off_type", 0)) if item.get("drop_off_type") else 0,
                        float(item.get("shape_dist_traveled", 0)) if item.get("shape_dist_traveled") else None
                    ))
                
                conn.commit()
                processed += 1
                
            except Exception as e:
                conn.rollback()
                # Log error but continue processing
                log(f"ERROR processing {table} item: {e}")
                continue
            finally:
                if cur:
                    cur.close()
        
        total_processed += processed
        
        if i % (batch_size * 10) == 0 and i > 0:
            log(f"Processed {total_processed}/{len(data)} {table} records...")
    
    log(f"Completed {table}: {total_processed}/{len(data)} records processed")
    return total_processed

# Backup
def create_tables_if_not_exist():
    """Create tables if they don't exist"""
    conn = None
    cur = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()
        
        # Create trips table
        cur.execute("""
            CREATE TABLE IF NOT EXISTS trips (
                trip_id TEXT PRIMARY KEY,
                route_id TEXT,
                service_id TEXT,
                trip_headsign TEXT,
                direction_id INT,
                block_id TEXT,
                shape_id TEXT,
                wheelchair_accessible INT,
                bikes_allowed INT
            );
        """)
        
        # Create stop_times table
        cur.execute("""
            CREATE TABLE IF NOT EXISTS stop_times (
                trip_id TEXT,
                arrival_time TEXT,
                departure_time TEXT,
                stop_id TEXT,
                stop_sequence INT,
                pickup_type INT DEFAULT 0,
                drop_off_type INT DEFAULT 0,
                shape_dist_traveled DOUBLE PRECISION,
                PRIMARY KEY (trip_id, stop_sequence)
            );
        """)
        
        # Create indexes for better performance
        cur.execute("CREATE INDEX IF NOT EXISTS idx_trips_route_id ON trips(route_id);")
        cur.execute("CREATE INDEX IF NOT EXISTS idx_trips_service_id ON trips(service_id);")
        cur.execute("CREATE INDEX IF NOT EXISTS idx_stop_times_stop_id ON stop_times(stop_id);")
        cur.execute("CREATE INDEX IF NOT EXISTS idx_stop_times_trip_id ON stop_times(trip_id);")
        cur.execute("CREATE INDEX IF NOT EXISTS idx_stop_times_departure_time ON stop_times(departure_time);")
        
        conn.commit()
        log("Database tables and indexes verified/created")
        
    except Exception as e:
        log(f"ERROR creating tables: {e}")
        if conn:
            conn.rollback()
    finally:
        if cur:
            cur.close()
        if conn:
            conn.close()

def run_cycle():
    """Run data fetch and update cycle with robust error handling"""
    global last_gtfs_api_update, last_gtfs_static_update
    
    try:
        current_time = time.time()
        should_update_api = (current_time - last_gtfs_api_update) >= GTFS_API_INTERVAL
        should_update_static = (current_time - last_gtfs_static_update) >= GTFS_STATIC_INTERVAL
        
        log("Starting data sync cycle")
        
        # Create tables if they don't exist
        create_tables_if_not_exist()
        
        conn = get_db_connection()
        total_inserts = 0
        
        try:
            # Update GTFS API data (stops & routes) every 12 hours
            if should_update_api:
                log("Updating GTFS API data (stops & routes)")
                
                # Process stops
                stops_data = fetch_gtfs_api_data("stops")
                if stops_data:
                    stops_processed = process_data_batch(conn, "stops", stops_data)
                    total_inserts += stops_processed
                    log(f"Processed {stops_processed} stops from API")

                # Process routes
                routes_data = fetch_gtfs_api_data("routes")
                if routes_data:
                    routes_processed = process_data_batch(conn, "routes", routes_data)
                    total_inserts += routes_processed
                    log(f"Processed {routes_processed} routes from API")
                
                last_gtfs_api_update = current_time
            else:
                next_api_update = int((GTFS_API_INTERVAL - (current_time - last_gtfs_api_update)) / 3600)
                log(f"Skipping GTFS API update (next update in ~{next_api_update} hours)")

            # Update GTFS static data (trips & stop_times) every 24 hours
            if should_update_static:
                log("Updating GTFS static data (trips & stop_times)")
                
                if download_gtfs_static():
                    # Process trips
                    trips_data = read_gtfs_csv("trips.txt")
                    if trips_data:
                        trips_processed = process_data_batch(conn, "trips", trips_data)
                        total_inserts += trips_processed
                        log(f"Processed {trips_processed} trips from static data")
                    
                    # Process stop_times (large dataset)
                    log("Processing stop_times.txt - this will take several minutes...")
                    stop_times_data = read_gtfs_csv("stop_times.txt")
                    if stop_times_data:
                        stop_times_processed = process_data_batch(conn, "stop_times", stop_times_data, batch_size=5000)
                        total_inserts += stop_times_processed
                        log(f"Processed {stop_times_processed} stop_times from static data")
                    
                    # Update stops/routes from static as backup
                    stops_static = read_gtfs_csv("stops.txt")
                    if stops_static:
                        stops_static_processed = process_data_batch(conn, "stops", stops_static)
                        log(f"Updated {stops_static_processed} stops from static backup")
                    
                    routes_static = read_gtfs_csv("routes.txt")
                    if routes_static:
                        routes_static_processed = process_data_batch(conn, "routes", routes_static)
                        log(f"Updated {routes_static_processed} routes from static backup")
                
                last_gtfs_static_update = current_time
            else:
                next_static_update = int((GTFS_STATIC_INTERVAL - (current_time - last_gtfs_static_update)) / 3600)
                log(f"Skipping GTFS static update (next update in ~{next_static_update} hours)")

            log(f"Cycle complete - Total processed: {total_inserts} records")
            
        finally:
            conn.close()

    except Exception as e:
        log(f"SYSTEM ERROR in cycle: {e}")

def main():
    """Main program loop with robust error handling"""
    log("AT API Cache Program v2 starting...")
    log(f"Database: {DB_CONFIG['host']}:{DB_CONFIG['port']}/{DB_CONFIG['dbname']}")
    log("Update intervals:")
    log("  - GTFS API (stops & routes): 12 hours")
    log("  - GTFS Static (trips & stop_times): 24 hours")
    log(f"  - Working directory: {GTFS_WORK_DIR}")
    
    check_interval = 3600  # 1 hour
    
    while True:
        try:
            run_cycle()
            log(f"Waiting {check_interval} seconds until next check...")
            time.sleep(check_interval)
        except KeyboardInterrupt:
            log("Program interrupted by user")
            break
        except Exception as e:
            log(f"UNEXPECTED ERROR in main loop: {e}")
            log(f"Waiting {check_interval} seconds before retry...")
            time.sleep(check_interval)
    
    log("AT API Cache Program stopped")

if __name__ == "__main__":
    main()
