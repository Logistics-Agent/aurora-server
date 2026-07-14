# Shipment Workflow Gap Analysis

## Baseline

Inspected source under `src/dotnet/ShipmentWorkflow`, `src/dotnet/Contracts/Shipment.Contracts`, `protos/shipment_workflow.proto`, and `tests/dotnet/ShipmentWorkflow.Tests` after Phase 18.

## Capability Matrix

| Capability | Status | Evidence |
| --- | --- | --- |
| Shipment aggregate | Partially Implemented | `Shipment` has CreateShipment-compatible fields plus Phase 11 MVP fields, child collections, and aggregate add methods. Full lifecycle behavior remains incomplete. |
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
| Outbox publication coverage | Implemented | Shipment command flows write required outbox records for create, submit, update, cancel, status changes, cargo updates, document attachment, pickup, delivery, and completion. RouteAssigned contract exists; no route assignment command is in current scope. |
| Expanded schema migration | Not Implemented | Phase 11 mapped the expanded model, but migration generation/application is intentionally deferred to Phase 19. |
| Integration tests for full MVP | Partially Implemented | 83 tests cover CreateShipment, validation, outbox, tenant isolation, aggregate child behavior, state-machine behavior, query behavior, command behavior, cargo/location management, document/milestone management, CSV import, and event serialization/outbox behavior. Full MVP test coverage remains Phase 19 scope. |

## Summary

CreateShipment vertical slice remains implemented and tested. Phase 11 expanded the aggregate model and EF mappings, and Phase 12 implemented the workflow state machine. Full Shipment Workflow MVP remains in progress and still requires migrations and full Phase 19 validation.
