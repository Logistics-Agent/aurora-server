# Shipment Workflow Gap Analysis

## Baseline

Inspected source under `src/dotnet/ShipmentWorkflow`, `src/dotnet/Contracts/Shipment.Contracts`, `protos/shipment_workflow.proto`, and `tests/dotnet/ShipmentWorkflow.Tests` after Phase 14.

## Capability Matrix

| Capability | Status | Evidence |
| --- | --- | --- |
| Shipment aggregate | Partially Implemented | `Shipment` has CreateShipment-compatible fields plus Phase 11 MVP fields, child collections, and aggregate add methods. Full lifecycle behavior remains incomplete. |
| Cargo/CargoItem | Partially Implemented | `CargoItem` remains compatible with CreateShipment. Full cargo fields and management commands remain later scope. |
| ShipmentLocation | Implemented | Entity, tenant ownership, validation, aggregate add method, DbSet, relationship, indexes, and tests exist. Management APIs are later scope. |
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
| Cargo update | Not Implemented | Create-only cargo behavior exists. |
| Location management | Not Implemented | Domain entity exists, but command/RPC management endpoints are not implemented. |
| Document metadata attachment | Not Implemented | Domain entity exists, but command/RPC attachment flow is not implemented. |
| Milestone creation | Partially Implemented | Business milestone entity exists; command/RPC creation flow remains later scope. |
| CSV/Excel import | Not Implemented | No import command/API found. |
| Required integration events | Partially Implemented | ShipmentCreated, ShipmentStatusChanged, ShipmentCancelled contracts exist. Remaining events missing. |
| Outbox publication coverage | Partially Implemented | CreateShipment writes ShipmentCreated outbox record. Worker/publisher not implemented. |
| Expanded schema migration | Not Implemented | Phase 11 mapped the expanded model, but migration generation/application is intentionally deferred to Phase 19. |
| Integration tests for full MVP | Partially Implemented | 56 tests cover CreateShipment, validation, outbox, tenant isolation, aggregate child behavior, state-machine behavior, query behavior, and Phase 14 command behavior. Full MVP test coverage remains later scope. |

## Summary

CreateShipment vertical slice remains implemented and tested. Phase 11 expanded the aggregate model and EF mappings, and Phase 12 implemented the workflow state machine. Full Shipment Workflow MVP remains in progress and still requires cargo/location/document/milestone management APIs, import, expanded contracts/events, migrations, and full test coverage.
