# Real-Time GPS Tracking & Geofencing Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & Spatial/IoT System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in .NET 10 `GpsTracking` implementation.

---

### Q1 (Junior): How does the service calculate whether a truck is inside a circular delivery zone?
**Answer**:  
The service uses the **Haversine formula** to calculate great-circle spherical distance between the vehicle's GPS coordinates and the geofence center point over the Earth's radius. If the calculated distance $d \le \text{GeofenceRadiusMeters}$, the truck is marked as inside.

---

### Q2 (Mid): How do you support complex, non-circular geofences like ports or logistics parks?
**Answer**:  
Polygon geofences are defined by an ordered series of vertex coordinates (WKT polygon). The service evaluates vehicle containment using the **Ray-Casting Point-in-Polygon (PIP) algorithm**: it projects an imaginary horizontal ray from the GPS coordinate to infinity and counts the number of times it intersects the polygon's edges. An odd intersection count means the vehicle is inside the polygon.

---

### Q3 (Mid): How does the system handle thousands of vehicle GPS pings without overloading the SQL database?
**Answer**:  
The architecture implements a **Hot/Cold Path Separation**:
- **Hot Path**: Incoming pings update Redis in-memory keys (`vehicle:{id}:pos`) in sub-milliseconds. Live map queries and dispatch screens read directly from Redis.
- **Cold Path**: Raw pings are queued and flushed to PostgreSQL in asynchronous bulk batches every few seconds for historical trail replay and compliance auditing.

---

### Q4 (Senior): How are geofence dwell times and departure events tracked?
**Answer**:  
The service maintains a state machine in `GeofencePresence`. When a vehicle transitions from outside to inside, it records `EnteredAt` and emits `GeofenceEnteredEvent`. Subsequent pings within the boundary update `DwellMinutes`. When a ping falls outside the boundary, it sets `ExitedAt`, calculates total duration, and publishes `GeofenceExitedEvent` to RabbitMQ.

---

### Q5 (System Design): What are the tradeoffs of computing geofences in the backend vs. on the driver mobile device?
**Answer**:  
- **Backend Computation**: Single source of truth, tamper-proof, allows retroactive geofencing over historical trails, and works with cheap IoT GPS dongles that lack compute power.
- **Client Computation**: Conserves battery and cellular data by only transmitting entry/exit events, but is vulnerable to device tampering and clock skew. Aurora uses backend calculation for enterprise auditability and hardware compatibility.
