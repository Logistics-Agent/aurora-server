# GPS Simulator Requirements Document

## Introduction

The GPS Simulator is a development and demonstration tool designed to simulate GPS device behavior for shipment tracking in the Aurora logistics platform. The simulator enables realistic demonstration of shipment movement along real road routes by consuming route geometry data and sending telemetry through existing GPS tracking infrastructure.

## Glossary

- **GPS_Simulator**: A command-line development tool that simulates GPS device telemetry for demonstration purposes
- **Shipment_Workflow_Service**: The Aurora microservice that manages shipment lifecycle and route assignments
- **Route_Planning_Service**: The Aurora microservice that manages route data and geometry
- **GPS_Tracking_Service**: The Aurora microservice that ingests and processes GPS position data
- **Route_Geometry**: The sequence of geographic coordinates (latitude, longitude) that define a route path
- **ROAD_Leg**: A segment of a shipment route that uses road transportation mode
- **Telemetry_Data**: GPS position data including coordinates, speed, heading, and timestamp
- **Demo_Mode**: Operation mode where the simulator runs without requiring external service connectivity

## Requirements

### Requirement 1: CLI Interface and Command Processing

**User Story:** As a developer, I want to run the GPS simulator via command line with configurable parameters, so that I can easily simulate different shipment scenarios.

#### Acceptance Criteria

1. WHEN the GPS_Simulator is invoked with `--shipment <shipmentId>`, THE GPS_Simulator SHALL accept the shipment identifier parameter
2. WHEN the GPS_Simulator is invoked with `--interval <seconds>`, THE GPS_Simulator SHALL set the telemetry transmission interval in seconds (minimum 1 second)
3. WHEN the GPS_Simulator is invoked with `--speed <kmh>`, THE GPS_Simulator SHALL set the base movement speed in kilometers per hour (minimum 10 km/h)
4. WHERE no interval is specified, THE GPS_Simulator SHALL default to 3 seconds between transmissions
5. WHERE no speed is specified, THE GPS_Simulator SHALL default to 50 km/h base speed
6. THE GPS_Simulator SHALL display usage help when invoked with invalid parameters

### Requirement 2: Shipment and Route Resolution

**User Story:** As a developer, I want the simulator to automatically resolve shipment routes through existing Aurora services, so that simulation follows real route data.

#### Acceptance Criteria

1. WHEN a shipment ID is provided, THE GPS_Simulator SHALL retrieve shipment details via Shipment_Workflow_Service gRPC API
2. WHEN shipment details are retrieved, THE GPS_Simulator SHALL extract the assigned route ID from the shipment data
3. WHEN a route ID is available, THE GPS_Simulator SHALL retrieve route details via Route_Planning_Service gRPC API
4. THE GPS_Simulator SHALL identify ROAD_Leg segments from the route data
5. IF no ROAD_Leg is found in the route, THEN THE GPS_Simulator SHALL terminate with an informative error message
6. THE GPS_Simulator SHALL extract Route_Geometry coordinates from the ROAD_Leg data

### Requirement 3: Route Geometry Processing and Movement Simulation

**User Story:** As a developer, I want the simulator to process route geometry into realistic movement patterns, so that the simulation appears natural and accurate.

#### Acceptance Criteria

1. WHEN Route_Geometry is retrieved, THE GPS_Simulator SHALL interpolate additional coordinate points between existing waypoints
2. THE GPS_Simulator SHALL calculate realistic heading values between consecutive coordinate points
3. THE GPS_Simulator SHALL apply speed variations around the base speed (±3 km/h random variation)
4. WHILE simulating movement, THE GPS_Simulator SHALL progress through coordinates in sequential order
5. THE GPS_Simulator SHALL calculate timestamp values for each position based on the configured interval
6. THE GPS_Simulator SHALL generate unique external reading IDs for each telemetry transmission

### Requirement 4: GPS Telemetry Transmission

**User Story:** As a developer, I want the simulator to send telemetry data through existing GPS tracking infrastructure, so that the simulation integrates with Aurora's tracking system.

#### Acceptance Criteria

1. WHEN telemetry data is prepared, THE GPS_Simulator SHALL transmit position data via GPS_Tracking_Service gRPC IngestPosition API
2. THE GPS_Simulator SHALL include required gRPC metadata headers (x-tenant-id, x-user-id)
3. THE GPS_Simulator SHALL populate all telemetry fields: coordinates, speed, heading, accuracy, and timestamp
4. THE GPS_Simulator SHALL use the shipment ID as the vehicle ID in telemetry data
5. THE GPS_Simulator SHALL generate synthetic device IDs in the format "sim-dev-{shipmentId}"
6. THE GPS_Simulator SHALL set accuracy to realistic values (2-5 meters)

### Requirement 5: Error Handling and Resilience

**User Story:** As a developer, I want the simulator to handle service unavailability gracefully, so that demonstration can continue even in offline scenarios.

#### Acceptance Criteria

1. IF Shipment_Workflow_Service is unavailable, THEN THE GPS_Simulator SHALL display a clear error message and terminate
2. IF Route_Planning_Service is unavailable, THEN THE GPS_Simulator SHALL display a clear error message and terminate
3. IF GPS_Tracking_Service is unavailable during transmission, THEN THE GPS_Simulator SHALL continue simulation in Demo_Mode
4. WHILE in Demo_Mode, THE GPS_Simulator SHALL log position data to console instead of transmitting
5. THE GPS_Simulator SHALL display connection status and service availability at startup
6. THE GPS_Simulator SHALL validate shipment ID format before making service calls

### Requirement 6: Configuration and Environment Support

**User Story:** As a developer, I want to configure service endpoints and authentication, so that the simulator can work across different environments.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL read gRPC service URLs from environment variables
2. THE GPS_Simulator SHALL support GPS_GRPC_URL environment variable for GPS tracking service endpoint
3. THE GPS_Simulator SHALL support SHIPMENT_GRPC_URL environment variable for shipment workflow service endpoint  
4. THE GPS_Simulator SHALL support ROUTE_GRPC_URL environment variable for route planning service endpoint
5. THE GPS_Simulator SHALL support TENANT_ID environment variable for multi-tenant operation
6. WHERE environment variables are not set, THE GPS_Simulator SHALL use default localhost development URLs

### Requirement 7: Simulation Progress and Feedback

**User Story:** As a developer, I want to monitor simulation progress and status, so that I can understand what the simulator is doing during execution.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL display startup information including shipment ID and configuration
2. WHILE simulating movement, THE GPS_Simulator SHALL display current position progress in format "[N/Total] lat,lng | speed km/h"
3. THE GPS_Simulator SHALL display route resolution status (shipment found, route retrieved, geometry processed)
4. THE GPS_Simulator SHALL display total number of coordinate points in the simulation
5. WHEN simulation completes, THE GPS_Simulator SHALL display completion message
6. THE GPS_Simulator SHALL display estimated simulation duration based on interval and coordinate count

### Requirement 8: Data Format Compliance and Integration

**User Story:** As a developer, I want the simulator to generate data compatible with Aurora's existing systems, so that simulated data integrates seamlessly with real tracking data.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL generate position timestamps in UTC format compatible with Google Protobuf Timestamp
2. THE GPS_Simulator SHALL format coordinate precision to 4 decimal places for display
3. THE GPS_Simulator SHALL generate external reading IDs using GUID format with "sim-" prefix
4. THE GPS_Simulator SHALL comply with GPS_Tracking_Service IngestPositionRequest message format
5. THE GPS_Simulator SHALL use consistent field naming and data types as defined in gps_tracking.proto
6. THE GPS_Simulator SHALL validate coordinate values are within valid geographic ranges (-90 to 90 latitude, -180 to 180 longitude)