# Auckland Transport Real-time Integration Module
# This module provides real-time data enhancement for the existing API

import os
import requests
import json
import time
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Any
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class ATRealtimeIntegration:
    """
    Auckland Transport Real-time API integration for enhancing static GTFS data
    with real-time information including delays, cancellations, and vehicle positions.
    """
    
    def __init__(self, api_key: str, cache_duration: int = 30):
        """
        Initialize the real-time integration module.
        
        Args:
            api_key: AT API subscription key
            cache_duration: Cache duration in seconds (default 30s to match AT update frequency)
        """
        self.api_key = api_key
        self.cache_duration = cache_duration
        self.base_url = "https://api.at.govt.nz/realtime/legacy"
        
        # Cache for real-time data
        self._trip_updates_cache = {"data": {}, "timestamp": None}
        self._vehicle_positions_cache = {"data": {}, "timestamp": None}
        self._service_alerts_cache = {"data": [], "timestamp": None}
        
        # Request headers
        self.headers = {
            'Cache-Control': 'no-cache',
            'Ocp-Apim-Subscription-Key': self.api_key,
            'Accept': 'application/json'
        }
    
    def _is_cache_valid(self, cache_entry: Dict) -> bool:
        """Check if cached data is still valid"""
        if not cache_entry["timestamp"]:
            return False
        # use total_seconds() to account for full delta, not just seconds component
        return (datetime.now() - cache_entry["timestamp"]).total_seconds() < self.cache_duration
    
    def _fetch_realtime_data(self, endpoint: str, params: Optional[Dict[str, str]] = None) -> Optional[Dict]:
        """
        Fetch data from AT Real-time API endpoint with error handling.
        
        Args:
            endpoint: API endpoint (tripupdates, vehiclelocations, servicealerts)
            params: Optional query parameters (e.g., {'tripid': 'xxx'})
            
        Returns:
            API response data or None if error occurs
        """
        try:
            url = f"{self.base_url}/{endpoint}"
            
            response = requests.get(url, headers=self.headers, params=params, timeout=10)
            response.raise_for_status()
            
            data = response.json()
            
            # Handle legacy API format: {"status": "OK", "response": {...}, "error": {}}
            if data.get("status") == "OK" and "response" in data:
                response_data = data["response"]
                entity_count = len(response_data.get('entity', []))
                logger.info(f"Fetched {entity_count} entities from {endpoint}")
                return response_data
            else:
                # Fallback to direct format
                logger.info(f"Fetched {len(data.get('entity', []))} entities from {endpoint}")
                return data
            
        except requests.exceptions.RequestException as e:
            logger.error(f"Error fetching {endpoint}: {e}")
            return None
        except json.JSONDecodeError as e:
            logger.error(f"Error parsing JSON from {endpoint}: {e}")
            return None
        except Exception as e:
            logger.error(f"Unexpected error fetching {endpoint}: {e}")
            return None
    
    def get_trip_updates(self, force_refresh: bool = False) -> Dict[str, Any]:
        """
        Get real-time trip updates (delays, cancellations).
        
        Args:
            force_refresh: Force refresh cache
            
        Returns:
            Dictionary mapping trip_id to trip update data
        """
        if not force_refresh and self._is_cache_valid(self._trip_updates_cache):
            return self._trip_updates_cache["data"]
        
        # Fetch fresh data
        data = self._fetch_realtime_data("tripupdates")
        if not data:
            return self._trip_updates_cache["data"]  # Return cached data on error
        
        # Process trip updates
        trip_updates = {}
        for entity in data.get("entity", []):
            if "trip_update" in entity:
                trip_update = entity["trip_update"]
                trip_id = trip_update.get("trip", {}).get("trip_id")
                
                if trip_id:
                    # Handle stop_time_update - can be a single dict or array
                    stop_time_update = trip_update.get("stop_time_update", [])
                    if isinstance(stop_time_update, dict):
                        # Single update - wrap in array
                        stop_time_update = [stop_time_update]
                    elif not isinstance(stop_time_update, list):
                        # Unexpected format
                        stop_time_update = []
                    
                    # Extract vehicle info from trip_update if available
                    vehicle_info = trip_update.get("vehicle", {})
                    
                    trip_updates[trip_id] = {
                        "timestamp": entity.get("timestamp"),
                        "delay": trip_update.get("delay", 0),
                        "schedule_relationship": trip_update.get("trip", {}).get("schedule_relationship", 0),
                        "stop_time_updates": self._process_stop_time_updates(stop_time_update),
                        "vehicle_id": vehicle_info.get("id"),
                        "vehicle_label": vehicle_info.get("label"),
                        "vehicle_license_plate": vehicle_info.get("license_plate")
                    }
        
        # Update cache
        self._trip_updates_cache = {
            "data": trip_updates,
            "timestamp": datetime.now()
        }
        
        logger.info(f"Cached {len(trip_updates)} trip updates")
        return trip_updates
    
    def get_trip_update_by_id(self, trip_id: str, use_cache_fallback: bool = True) -> Optional[Dict[str, Any]]:
        """
        Get real-time trip update for a specific trip using API filtering.
        Falls back to cached data if API fails.
        
        Args:
            trip_id: Trip ID to get update for
            use_cache_fallback: If True, check cache if fresh API call fails
            
        Returns:
            Trip update data or None if not found
        """
        # Try to fetch fresh data from API with trip filter
        data = self._fetch_realtime_data("tripupdates", params={"tripid": trip_id})
        
        if not data:
            # API call failed - try cache as fallback
            if use_cache_fallback and self._is_cache_valid(self._trip_updates_cache):
                cached_update = self._trip_updates_cache["data"].get(trip_id)
                if cached_update:
                    logger.info(f"Using cached data for trip {trip_id} (API unavailable)")
                    return cached_update
            logger.warning(f"No realtime data available for trip {trip_id} (API failed, no cache)")
            return None
        
        # Process first (and should be only) entity
        entities = data.get("entity", [])
        if not entities:
            # No data returned - check cache
            if use_cache_fallback and self._is_cache_valid(self._trip_updates_cache):
                cached_update = self._trip_updates_cache["data"].get(trip_id)
                if cached_update:
                    logger.info(f"Using cached data for trip {trip_id} (not in fresh API response)")
                    return cached_update
            return None
        
        entity = entities[0]
        if "trip_update" in entity:
            trip_update = entity["trip_update"]
            
            # Handle stop_time_update - can be a single dict or array
            stop_time_update = trip_update.get("stop_time_update", [])
            if isinstance(stop_time_update, dict):
                stop_time_update = [stop_time_update]
            elif not isinstance(stop_time_update, list):
                stop_time_update = []
            
            # Extract vehicle info from trip_update if available
            vehicle_info = trip_update.get("vehicle", {})
            
            return {
                "timestamp": entity.get("timestamp"),
                "delay": trip_update.get("delay", 0),
                "schedule_relationship": trip_update.get("trip", {}).get("schedule_relationship", 0),
                "stop_time_updates": self._process_stop_time_updates(stop_time_update),
                "vehicle_id": vehicle_info.get("id"),
                "vehicle_label": vehicle_info.get("label"),
                "vehicle_license_plate": vehicle_info.get("license_plate")
            }
        
        return None
    
    def _apply_delay_to_gtfs_time(self, time_str: Optional[str], delay_seconds: int) -> Optional[str]:
        """
        Apply delay in seconds to a GTFS time string.
        
        Args:
            time_str: Time string in HH:MM:SS format
            delay_seconds: Delay in seconds (positive = late, negative = early)
            
        Returns:
            Updated time string in HH:MM:SS format
        """
        if not time_str:
            return time_str
        
        try:
            parts = time_str.split(":")
            if len(parts) != 3:
                return time_str
            
            hours, minutes, seconds = map(int, parts)
            total_seconds = hours * 3600 + minutes * 60 + seconds + delay_seconds
            
            # Handle negative times (shouldn't happen but just in case)
            if total_seconds < 0:
                total_seconds = 0
            
            new_hours = total_seconds // 3600
            new_minutes = (total_seconds % 3600) // 60
            new_seconds = total_seconds % 60
            
            return f"{new_hours:02d}:{new_minutes:02d}:{new_seconds:02d}"
        except (ValueError, AttributeError):
            return time_str
    
    def _process_stop_time_updates(self, stop_time_updates: List[Dict]) -> Dict[str, Any]:
        """Process stop time updates into a more usable format"""
        processed = {}
        for update in stop_time_updates:
            stop_id = update.get("stop_id")
            if stop_id:
                processed[stop_id] = {
                    "stop_sequence": update.get("stop_sequence"),
                    "arrival": {
                        "delay": update.get("arrival", {}).get("delay", 0),
                        "time": update.get("arrival", {}).get("time"),
                        "uncertainty": update.get("arrival", {}).get("uncertainty")
                    },
                    "departure": {
                        "delay": update.get("departure", {}).get("delay", 0),
                        "time": update.get("departure", {}).get("time"),
                        "uncertainty": update.get("departure", {}).get("uncertainty")
                    },
                    "schedule_relationship": update.get("schedule_relationship", 0)
                }
        return processed
    
    def get_vehicle_positions(self, force_refresh: bool = False) -> Dict[str, Any]:
        """
        Get real-time vehicle positions.
        
        Args:
            force_refresh: Force refresh cache
            
        Returns:
            Dictionary mapping vehicle_id to position data
        """
        if not force_refresh and self._is_cache_valid(self._vehicle_positions_cache):
            return self._vehicle_positions_cache["data"]
        
        # Fetch fresh data
        data = self._fetch_realtime_data("vehiclelocations")
        if not data:
            return self._vehicle_positions_cache["data"]  # Return cached data on error
        
        # Process vehicle positions
        vehicle_positions = {}
        for entity in data.get("entity", []):
            if "vehicle" in entity and not entity.get("is_deleted", False):
                vehicle = entity["vehicle"]
                vehicle_info = vehicle.get("vehicle", {})
                vehicle_id = vehicle_info.get("id")
                
                if vehicle_id:
                    position = vehicle.get("position", {})
                    trip_info = vehicle.get("trip", {})
                    
                    vehicle_positions[vehicle_id] = {
                        "timestamp": entity.get("timestamp"),
                        "trip_id": trip_info.get("trip_id"),
                        "route_id": trip_info.get("route_id"),
                        "start_time": trip_info.get("start_time"),
                        "schedule_relationship": trip_info.get("schedule_relationship", 0),
                        "position": {
                            "latitude": position.get("latitude"),
                            "longitude": position.get("longitude"),
                            "bearing": position.get("bearing"),
                            "speed": position.get("speed")
                        },
                        "current_stop_sequence": vehicle.get("current_stop_sequence"),
                        "current_status": vehicle.get("current_status"),
                        "congestion_level": vehicle.get("congestion_level"),
                        "occupancy_status": vehicle.get("occupancy_status"),
                        "license_plate": vehicle_info.get("license_plate"),
                        "wheelchair_accessible": vehicle_info.get("wheelchair_accessible")
                    }
        
        # Update cache
        self._vehicle_positions_cache = {
            "data": vehicle_positions,
            "timestamp": datetime.now()
        }
        
        logger.info(f"Cached {len(vehicle_positions)} vehicle positions")
        return vehicle_positions
    
    def get_vehicle_positions_for_trip(self, trip_id: str, force_refresh: bool = False) -> Dict[str, Any]:
        """
        Get real-time vehicle positions for a specific trip using API filtering.
        
        Args:
            trip_id: Trip ID to get vehicle position for
            force_refresh: Force refresh cache (ignored, always fetches fresh for specific trip)
            
        Returns:
            Dictionary mapping vehicle_id to position data for this trip only
        """
        # Fetch vehicle positions filtered by trip ID
        data = self._fetch_realtime_data("vehiclelocations", params={"tripid": trip_id})
        if not data:
            return {}
        
        # Process vehicle positions
        vehicle_positions = {}
        for entity in data.get("entity", []):
            if "vehicle" in entity and not entity.get("is_deleted", False):
                vehicle = entity["vehicle"]
                vehicle_info = vehicle.get("vehicle", {})
                vehicle_id = vehicle_info.get("id")
                
                if vehicle_id:
                    position = vehicle.get("position", {})
                    trip_info = vehicle.get("trip", {})
                    
                    vehicle_positions[vehicle_id] = {
                        "timestamp": entity.get("timestamp"),
                        "trip_id": trip_info.get("trip_id"),
                        "route_id": trip_info.get("route_id"),
                        "start_time": trip_info.get("start_time"),
                        "schedule_relationship": trip_info.get("schedule_relationship", 0),
                        "position": {
                            "latitude": position.get("latitude"),
                            "longitude": position.get("longitude"),
                            "bearing": position.get("bearing"),
                            "speed": position.get("speed")
                        },
                        "current_stop_sequence": vehicle.get("current_stop_sequence"),
                        "current_status": vehicle.get("current_status"),
                        "congestion_level": vehicle.get("congestion_level"),
                        "occupancy_status": vehicle.get("occupancy_status"),
                        "license_plate": vehicle_info.get("license_plate"),
                        "wheelchair_accessible": vehicle_info.get("wheelchair_accessible")
                    }
        
        logger.info(f"Fetched {len(vehicle_positions)} vehicle positions for trip {trip_id}")
        return vehicle_positions
    
    def get_vehicle_position_by_id(self, vehicle_id: str) -> Optional[Dict[str, Any]]:
        """
        Get real-time vehicle position for a specific vehicle using API filtering.
        
        Args:
            vehicle_id: Vehicle ID to get position for
            
        Returns:
            Vehicle position data or None if not found
        """
        # Fetch vehicle position filtered by vehicle ID
        data = self._fetch_realtime_data("vehiclelocations", params={"vehicleid": vehicle_id})
        if not data:
            return None
        
        # Process first (and should be only) entity
        entities = data.get("entity", [])
        if not entities:
            return None
        
        entity = entities[0]
        if "vehicle" in entity and not entity.get("is_deleted", False):
            vehicle = entity["vehicle"]
            position = vehicle.get("position", {})
            
            return {
                "timestamp": entity.get("timestamp"),
                "position": {
                    "latitude": position.get("latitude"),
                    "longitude": position.get("longitude"),
                    "bearing": position.get("bearing"),
                    "speed": position.get("speed")
                },
                "current_stop_sequence": vehicle.get("current_stop_sequence"),
                "current_status": vehicle.get("current_status"),
                "congestion_level": vehicle.get("congestion_level"),
                "occupancy_status": vehicle.get("occupancy_status")
            }
        
        return None
    
    def get_service_alerts(self, force_refresh: bool = False) -> List[Dict]:
        """
        Get real-time service alerts.
        
        Args:
            force_refresh: Force refresh cache
            
        Returns:
            List of service alert objects
        """
        if not force_refresh and self._is_cache_valid(self._service_alerts_cache):
            return self._service_alerts_cache["data"]
        
        # Fetch fresh data
        data = self._fetch_realtime_data("servicealerts")
        if not data:
            return self._service_alerts_cache["data"]  # Return cached data on error
        
        # Process service alerts
        alerts = []
        for entity in data.get("entity", []):
            if "alert" in entity:
                alert = entity["alert"]
                alerts.append({
                    "id": entity.get("id"),
                    "active_period": alert.get("active_period", []),
                    "informed_entity": alert.get("informed_entity", []),
                    "cause": alert.get("cause"),
                    "effect": alert.get("effect"),
                    "url": alert.get("url", {}).get("translation", [{}])[0].get("text", ""),
                    "header_text": alert.get("header_text", {}).get("translation", [{}])[0].get("text", ""),
                    "description_text": alert.get("description_text", {}).get("translation", [{}])[0].get("text", ""),
                    "severity_level": alert.get("severity_level")
                })
        
        # Update cache
        self._service_alerts_cache = {
            "data": alerts,
            "timestamp": datetime.now()
        }
        
        logger.info(f"Cached {len(alerts)} service alerts")
        return alerts
    
    def enhance_stop_times_with_realtime(self, stop_times: List[Dict], stop_id: str = None, force_fresh: bool = False) -> List[Dict]:
        """
        Enhance static stop times with real-time delay information.
        Replaces scheduled times with real-time predictions.
        
        Args:
            stop_times: List of stop time records from your API
            stop_id: Optional stop ID to filter real-time data
            force_fresh: If True, fetch fresh data for each trip instead of using cache
            
        Returns:
            Enhanced stop times with real-time data (times replaced)
        """
        if force_fresh:
            # Fetch fresh realtime for each unique trip
            enhanced_times = []
            for stop_time in stop_times:
                enhanced = stop_time.copy()
                trip_id = stop_time.get("trip_id")
                
                if trip_id:
                    # Get fresh realtime data with cache fallback
                    trip_update = self.get_trip_update_by_id(trip_id, use_cache_fallback=True)
                    
                    if trip_update:
                        delay = trip_update.get("delay", 0)
                        
                        # Check for stop-specific updates first
                        stop_updates = trip_update.get("stop_time_updates", {})
                        if stop_id and stop_id in stop_updates:
                            stop_update = stop_updates[stop_id]
                            arrival_delay = stop_update["arrival"]["delay"]
                            departure_delay = stop_update["departure"]["delay"]
                            
                            # Replace with predicted times if available, otherwise apply delay
                            if stop_update["arrival"].get("time"):
                                enhanced["arrival_time"] = stop_update["arrival"]["time"]
                            else:
                                enhanced["arrival_time"] = self._apply_delay_to_gtfs_time(
                                    enhanced.get("arrival_time"), arrival_delay
                                )
                            
                            if stop_update["departure"].get("time"):
                                enhanced["departure_time"] = stop_update["departure"]["time"]
                            else:
                                enhanced["departure_time"] = self._apply_delay_to_gtfs_time(
                                    enhanced.get("departure_time"), departure_delay
                                )
                        else:
                            # Use trip-level delay to adjust times
                            enhanced["arrival_time"] = self._apply_delay_to_gtfs_time(
                                enhanced.get("arrival_time"), delay
                            )
                            enhanced["departure_time"] = self._apply_delay_to_gtfs_time(
                                enhanced.get("departure_time"), delay
                            )
                        
                        enhanced["is_realtime"] = True
                        enhanced["delay_seconds"] = delay
                    else:
                        enhanced["is_realtime"] = False
                else:
                    enhanced["is_realtime"] = False
                
                enhanced_times.append(enhanced)
            
            return enhanced_times
        else:
            # Use cached bulk data (original behavior for non-active trips)
            trip_updates = self.get_trip_updates()
            enhanced_times = []
            
            for stop_time in stop_times:
                enhanced = stop_time.copy()
                trip_id = stop_time.get("trip_id")
                
                # Check for real-time updates in cache
                if trip_id in trip_updates:
                    trip_update = trip_updates[trip_id]
                    delay = trip_update.get("delay", 0)
                    
                    # Check for stop-specific updates
                    stop_updates = trip_update.get("stop_time_updates", {})
                    if stop_id and stop_id in stop_updates:
                        stop_update = stop_updates[stop_id]
                        arrival_delay = stop_update["arrival"]["delay"]
                        departure_delay = stop_update["departure"]["delay"]
                        
                        # Replace with predicted times if available, otherwise apply delay
                        if stop_update["arrival"].get("time"):
                            enhanced["arrival_time"] = stop_update["arrival"]["time"]
                        else:
                            enhanced["arrival_time"] = self._apply_delay_to_gtfs_time(
                                enhanced.get("arrival_time"), arrival_delay
                            )
                        
                        if stop_update["departure"].get("time"):
                            enhanced["departure_time"] = stop_update["departure"]["time"]
                        else:
                            enhanced["departure_time"] = self._apply_delay_to_gtfs_time(
                                enhanced.get("departure_time"), departure_delay
                            )
                    else:
                        # Use trip-level delay
                        enhanced["arrival_time"] = self._apply_delay_to_gtfs_time(
                            enhanced.get("arrival_time"), delay
                        )
                        enhanced["departure_time"] = self._apply_delay_to_gtfs_time(
                            enhanced.get("departure_time"), delay
                        )
                    
                    enhanced["is_realtime"] = True
                    enhanced["delay_seconds"] = delay
                else:
                    enhanced["is_realtime"] = False
                
                enhanced_times.append(enhanced)
            
            return enhanced_times
    
    def get_trip_with_realtime(self, trip_id: str, static_trip_data: Dict, force_refresh: bool = False) -> Dict:
        """
        Enhance a single trip with real-time information.
        Note: This doesn't modify times since trips don't have time fields in GTFS.
        
        Args:
            trip_id: Trip ID to enhance
            static_trip_data: Static trip data from your API
            force_refresh: If True, fetch fresh data; if False, use cached bulk data
            
        Returns:
            Enhanced trip data with real-time information
        """
        enhanced = static_trip_data.copy()
        
        if force_refresh:
            # Trip is active - get fresh realtime data with cache fallback
            trip_update = self.get_trip_update_by_id(trip_id, use_cache_fallback=True)
            
            if trip_update and isinstance(trip_update, dict):
                enhanced["is_realtime"] = True
                enhanced["delay_seconds"] = trip_update.get("delay", 0)
                enhanced["schedule_relationship"] = trip_update.get("schedule_relationship", 0)
                enhanced["last_updated"] = trip_update.get("timestamp")
                enhanced["data_source"] = "fresh_api"
                
                # Get vehicle position if vehicle_id is available
                vehicle_id = trip_update.get("vehicle_id")
                if vehicle_id:
                    vehicle_position = self.get_vehicle_position_by_id(vehicle_id)
                    if vehicle_position:
                        enhanced["vehicle"] = {
                            "id": vehicle_id,
                            "label": trip_update.get("vehicle_label", ""),
                            "license_plate": trip_update.get("vehicle_license_plate", ""),
                            "position": vehicle_position.get("position", {}),
                            "current_stop_sequence": vehicle_position.get("current_stop_sequence"),
                            "current_status": vehicle_position.get("current_status"),
                            "occupancy_status": vehicle_position.get("occupancy_status")
                        }
            else:
                enhanced["is_realtime"] = False
                enhanced["data_source"] = "none"
        else:
            # Trip is inactive - use cached bulk data (more efficient)
            trip_updates = self.get_trip_updates(force_refresh=False)
            
            if trip_id in trip_updates:
                trip_update = trip_updates[trip_id]
                if isinstance(trip_update, dict):
                    enhanced["is_realtime"] = True
                    enhanced["delay_seconds"] = trip_update.get("delay", 0)
                    enhanced["schedule_relationship"] = trip_update.get("schedule_relationship", 0)
                    enhanced["last_updated"] = trip_update.get("timestamp")
                    enhanced["data_source"] = "cached"
                    
                    # Get vehicle position if vehicle_id is available
                    vehicle_id = trip_update.get("vehicle_id")
                    if vehicle_id:
                        vehicle_position = self.get_vehicle_position_by_id(vehicle_id)
                        if vehicle_position:
                            enhanced["vehicle"] = {
                                "id": vehicle_id,
                                "label": trip_update.get("vehicle_label", ""),
                                "license_plate": trip_update.get("vehicle_license_plate", ""),
                                "position": vehicle_position.get("position", {}),
                                "current_stop_sequence": vehicle_position.get("current_stop_sequence"),
                                "current_status": vehicle_position.get("current_status"),
                                "occupancy_status": vehicle_position.get("occupancy_status")
                            }
                else:
                    logger.warning(f"Trip update for {trip_id} is not a dict: {type(trip_update)}")
                    enhanced["is_realtime"] = False
                    enhanced["data_source"] = "cached"
            else:
                enhanced["is_realtime"] = False
                enhanced["data_source"] = "cached"
        
        return enhanced
    
    def get_alerts_for_stop(self, stop_id: str) -> List[Dict]:
        """
        Get service alerts affecting a specific stop.
        
        Args:
            stop_id: Stop ID to check for alerts
            
        Returns:
            List of relevant service alerts
        """
        all_alerts = self.get_service_alerts()
        relevant_alerts = []
        
        for alert in all_alerts:
            # Check if alert affects this stop
            for entity in alert.get("informed_entity", []):
                if entity.get("stop_id") == stop_id:
                    relevant_alerts.append(alert)
                    break
        
        return relevant_alerts
    
    def get_alerts_for_route(self, route_id: str) -> List[Dict]:
        """
        Get service alerts affecting a specific route.
        
        Args:
            route_id: Route ID to check for alerts
            
        Returns:
            List of relevant service alerts
        """
        all_alerts = self.get_service_alerts()
        relevant_alerts = []
        
        for alert in all_alerts:
            # Check if alert affects this route
            for entity in alert.get("informed_entity", []):
                if entity.get("route_id") == route_id:
                    relevant_alerts.append(alert)
                    break
        
        return relevant_alerts

# Example usage and configuration
if __name__ == "__main__":
    # Your API key from the existing cache program
    # Prefer environment variable to avoid committing secrets
    API_KEY = os.environ.get("AT_API_KEY", "25c926c6234a49c98d52d90a8bd7ac7e")
    
    # Initialize the real-time integration
    rt_integration = ATRealtimeIntegration(API_KEY)
    
    # Test fetching real-time data
    print("Testing real-time integration...")
    
    # Get trip updates
    trip_updates = rt_integration.get_trip_updates()
    print(f"Found {len(trip_updates)} trip updates")
    
    # Get vehicle positions  
    vehicle_positions = rt_integration.get_vehicle_positions()
    print(f"Found {len(vehicle_positions)} vehicle positions")
    
    # Get service alerts
    alerts = rt_integration.get_service_alerts()
    print(f"Found {len(alerts)} service alerts")