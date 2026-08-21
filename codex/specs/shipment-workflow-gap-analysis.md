# Shipment Workflow Gap Analysis

## Baseline

Inspected source under `src/dotnet/ShipmentWorkflow`, `src/dotnet/Contracts/Shipment.Contracts`, `protos/shipment_workflow.proto`, and `src/dotnet/ShipmentWorkflow/Tests` after Phase 19.

## Capability Matrix

| Capability | Status | Evidence |
| --- | --- | --- |
| Shipment aggregate | Implemented | Shipment aggregate includes MVP fields, child collections, lifecycle state machine, cargo/location/document/milestone behavior, and tenant ownership. |
| Cargo/CargoItem | Implemented | Cargo items support create/update/remove through tenant-safe commands and gRPC endpoints, validation, and CargoUpdated outbox records. |
| ShipmentLocation | Implemented | Entity, tenant ownership, validation, aggregate add/update/remove commands, gRPC endpoints, DbSet, relationship, indexes, and tests exist. |
| ShipmentDocument | Implemented | Entity stores metadata/reference only, OCR status/confidence, optional extracted JSON, tenant ownership, DbSet, relationship, indexes, and tests. OCR processing is out of scope. |
| ShipmentMilestone | Implemented | Entity stores business milestone metadata with source, recorded time, optional coordinates, tenant ownership, DbSet, relationship, indexes, and tests. Detailed GPS history remains out of scope. |
| ShipmentPriority | Implemented | Enum exists and is mapped as a string. |
| TransportMode | Implemented | Enum exists and is mapped as a string. |
| LocationType | Implemented | Enum exists and is mapped as a string. |
| DocumentType | Implemented | Enum exists and is mapped as a string. |
| OCRStatus | Implemented | Enum exists and is mapped as a string. |
| MilestoneSource | Implemented | Enum exists and is mapped as a string. |
| CustomerId | Implemented | Nullable external customer ID exists on Shipment and is indexed with TenantId. |
| RouteId | Implemented | Nullable external route ID exists on Shipment and is indexed with TenantId. |
| VehicleId | Implemented | Nullable external vehicle ID exists on Shipment and is indexed with TenantId. |
| Pickup/delivery timestamps | Implemented | Estimated and actual pickup/delivery timestamps exist on Shipment. |
| Notes | Implemented | Optional notes field exists on Shipment. |
| State-machine transition validation | Implemented | Shipment aggregate enforces explicit allowed transitions, terminal states, cancellation rules, status history, milestones, and pickup/delivery timestamps. |
| CreateShipment | Implemented | gRPC maps to command, command persists shipment/cargo/history/outbox, tests pass. |
| GetShipment | Implemented | Tenant-safe query handler and gRPC method return aggregate data or NotFound without cross-tenant leakage. |
| ListShipments | Implemented | Tenant-safe paginated list with status, shipment number, customer, and date filters. |
| SubmitShipment | Implemented | Command and gRPC method submit via validated state-machine transition and outbox status event. |
| UpdateShipment | Implemented | Command and gRPC method update editable fields before operational processing starts. |
| UpdateShipmentStatus | Implemented | Command and gRPC method map requested status to validated state-machine transition. |
| GetShipmentTimeline | Implemented | Tenant-safe timeline combines status histories and business milestones in deterministic order. |
| CancelShipment | Implemented | Command and gRPC method cancel allowed states and write status/cancellation outbox records. |
| Draft deletion | Implemented | Command and gRPC method delete only draft/created shipments. |
| Cargo update | Implemented | Add, update, and remove cargo commands/RPCs exist with validation, tenant isolation, mutation restrictions, and CargoUpdated outbox messages. |
| Location management | Implemented | Add, update, and remove location commands/RPCs exist with sequence validation, coordinate validation, tenant isolation, and mutation restrictions. |
| Document metadata attachment | Implemented | Attach, update OCR metadata, and remove document metadata commands/RPCs exist with validation, tenant isolation, and DocumentAttached outbox messages. |
| Milestone creation | Implemented | Business milestone command/RPC exists with source, recorded time, coordinate validation, tenant isolation, and timeline coverage. |
| CSV/Excel import | Implemented | CSV import command/RPC exists with row-level results, file/row limits, TenantId rejection, partial-success policy, tenant isolation, and ShipmentCreated outbox writes. Excel remains out of scope for MVP. |
| Required integration events | Implemented | ShipmentCreated, ShipmentSubmitted, ShipmentUpdated, ShipmentCancelled, ShipmentStatusChanged, CargoUpdated, DocumentAttached, RouteAssigned, ShipmentPickedUp, ShipmentDelivered, and ShipmentCompleted contracts exist with EventId and ContractVersion fields. |
| Outbox publication coverage | Implemented | Shipment command flows write required outbox records and Phase 20 publishes all 11 allowlisted contract types through MassTransit/RabbitMQ. PostgreSQL skip-locked batching prevents workers from claiming the same rows; failures remain pending with bounded retry and diagnostics. RouteAssigned contract exists; no route assignment command is in current scope. |
| Shipment-to-Notification delivery | Implemented | Local runtime smoke proved a ShipmentCreated outbox row was marked processed, consumed idempotently by Notification, and projected to a sent InApp notification without cross-database service access. |
| Expanded schema migration | Implemented | `20260714042938_ExpandShipmentWorkflowMvp` was generated and applied to local `aurora_shipment_workflow`. |
| Integration tests for full MVP | Implemented | 99 tests cover CreateShipment, validation, outbox publication/retry, tenant isolation, aggregate child behavior, state-machine behavior, query behavior, command behavior, cargo/location management, document/milestone management, CSV import, event serialization/outbox behavior, and migration-compatible persistence. |

## Summary

CreateShipment vertical slice remains implemented and tested. Shipment Workflow full MVP plus broker-backed outbox publication are complete, the expanded migration remains applied, and 99 tests pass.
