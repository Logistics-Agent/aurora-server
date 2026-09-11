# GPS Simulator Requirements Document

## Introduction

The GPS Simulator is a development and demonstration tool designed to simulate GPS device behavior for shipment tracking in the Aurora logistics platform. The simulator enables realistic demonstration of shipment movement using hardcoded realistic routes and sends telemetry through existing GPS tracking infrastructure for demo and testing purposes.

## Glossary

- **GPS_Simulator**: A command-line development tool that simulates GPS device telemetry for demonstration purposes
- **Shipment_Workflow_Service**: The Aurora microservice that manages shipment lifecycle and route assignments
- **GPS_Tracking_Service**: The Aurora microservice that ingests and processes GPS position data
- **Telemetry_Data**: GPS position data including coordinates, speed, heading, and timestamp
- **Central_America_Route**: A hardcoded realistic route from San José, Costa Rica to Panama City, Panama

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

### Requirement 2: Shipment Validation

**User Story:** As a developer, I want the simulator to validate that the shipment exists before starting simulation, so that I can ensure I'm working with valid shipment data.

#### Acceptance Criteria

1. WHEN a shipment ID is provided, THE GPS_Simulator SHALL retrieve shipment details via Shipment_Workflow_Service gRPC API
2. IF the shipment exists, THEN THE GPS_Simulator SHALL display shipment information and proceed with simulation
3. IF the shipment does not exist, THEN THE GPS_Simulator SHALL display an error message and terminate
4. THE GPS_Simulator SHALL validate shipment ID format before making service calls
5. THE GPS_Simulator SHALL display connection status to the Shipment_Workflow_Service at startup

### Requirement 3: Hardcoded Route Processing and Movement Simulation

**User Story:** As a developer, I want the simulator to use a realistic hardcoded route for demonstration purposes, so that I can showcase GPS tracking functionality without dependency on route geometry services.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL use a predefined Central_America_Route from San José, Costa Rica to Panama City, Panama
2. THE GPS_Simulator SHALL interpolate additional coordinate points between hardcoded waypoints to create smooth movement
3. THE GPS_Simulator SHALL calculate realistic heading values between consecutive coordinate points
4. THE GPS_Simulator SHALL apply speed variations around the base speed (±3 km/h random variation)
5. WHILE simulating movement, THE GPS_Simulator SHALL progress through coordinates in sequential order
6. THE GPS_Simulator SHALL calculate timestamp values for each position based on the configured interval
7. THE GPS_Simulator SHALL generate unique external reading IDs for each telemetry transmission

### Requirement 4: GPS Telemetry Transmission

**User Story:** As a developer, I want the simulator to send telemetry data through existing GPS tracking infrastructure, so that the simulation integrates with Aurora's tracking system.

#### Acceptance Criteria

1. WHEN telemetry data is prepared, THE GPS_Simulator SHALL transmit position data via GPS_Tracking_Service gRPC IngestPosition API
2. THE GPS_Simulator SHALL include required gRPC metadata headers (x-tenant-id, x-user-id)
3. THE GPS_Simulator SHALL populate all telemetry fields: coordinates, speed, heading, accuracy, and timestamp
4. THE GPS_Simulator SHALL use the shipment ID as the vehicle ID in telemetry data
5. THE GPS_Simulator SHALL generate synthetic device IDs in the format "sim-dev-{shipmentId}"
6. THE GPS_Simulator SHALL set accuracy to realistic values (2-5 meters)

### Requirement 5: Basic Error Handling

**User Story:** As a developer, I want the simulator to handle common error conditions gracefully, so that I get clear feedback when something goes wrong.

#### Acceptance Criteria

1. IF Shipment_Workflow_Service is unavailable, THEN THE GPS_Simulator SHALL display a clear error message and terminate
2. IF GPS_Tracking_Service is unavailable during transmission, THEN THE GPS_Simulator SHALL continue simulation displaying position data to console
3. THE GPS_Simulator SHALL validate shipment ID format before making service calls
4. THE GPS_Simulator SHALL display clear error messages for invalid command line parameters
5. THE GPS_Simulator SHALL handle network timeouts gracefully with informative error messages

### Requirement 6: Basic Configuration Support

**User Story:** As a developer, I want to configure service endpoints and tenant information, so that the simulator can work in different environments.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL support GPS_GRPC_URL environment variable for GPS tracking service endpoint
2. THE GPS_Simulator SHALL support SHIPMENT_GRPC_URL environment variable for shipment workflow service endpoint  
3. THE GPS_Simulator SHALL support TENANT_ID environment variable for multi-tenant operation
4. WHERE environment variables are not set, THE GPS_Simulator SHALL use default localhost development URLs
5. THE GPS_Simulator SHALL display current configuration settings at startup

### Requirement 7: Console Progress Display

**User Story:** As a developer, I want to monitor simulation progress, so that I can understand what the simulator is doing during execution.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL display startup information including shipment ID and configuration
2. WHILE simulating movement, THE GPS_Simulator SHALL display current position progress in format "[N/Total] lat,lng | speed km/h"
3. THE GPS_Simulator SHALL display shipment validation status (found/not found)
4. THE GPS_Simulator SHALL display total number of coordinate points in the simulation
5. WHEN simulation completes, THE GPS_Simulator SHALL display completion message

### Requirement 8: Data Format Compliance and Integration

**User Story:** As a developer, I want the simulator to generate data compatible with Aurora's existing systems, so that simulated data integrates seamlessly with real tracking data.

#### Acceptance Criteria

1. THE GPS_Simulator SHALL generate position timestamps in UTC format compatible with Google Protobuf Timestamp
2. THE GPS_Simulator SHALL format coordinate precision to 4 decimal places for display
3. THE GPS_Simulator SHALL generate external reading IDs using GUID format with "sim-" prefix
4. THE GPS_Simulator SHALL comply with GPS_Tracking_Service IngestPositionRequest message format
5. THE GPS_Simulator SHALL use consistent field naming and data types as defined in gps_tracking.proto
6. THE GPS_Simulator SHALL validate coordinate values are within valid geographic ranges (-90 to 90 latitude, -180 to 180 longitude)