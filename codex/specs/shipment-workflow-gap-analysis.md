# Shipment Workflow Gap Analysis

## Baseline

Inspected source under `src/dotnet/ShipmentWorkflow`, `src/dotnet/Contracts/Shipment.Contracts`, `protos/shipment_workflow.proto`, and `tests/dotnet/ShipmentWorkflow.Tests` after Phase 10.

## Capability Matrix

| Capability | Status | Evidence |
| --- | --- | --- |
| Shipment aggregate | Partially Implemented | `Shipment` exists with minimal fields and Create/ChangeStatus/AddCargoItem. Full MVP fields are missing. |
| Cargo/CargoItem | Partially Implemented | `CargoItem` exists with Name, Quantity, WeightKg, HsCode. Full cargo fields are missing. |
| ShipmentLocation | Not Implemented | No entity or DbSet found. |
| ShipmentDocument | Not Implemented | No entity or DbSet found. |
| ShipmentMilestone | Not Implemented | No entity or DbSet found. |
| ShipmentPriority | Not Implemented | No enum found. |
| TransportMode | Not Implemented | No enum found. |
| LocationType | Not Implemented | No enum found. |
| DocumentType | Not Implemented | No enum found. |
| OCRStatus | Not Implemented | No enum found. |
| MilestoneSource | Not Implemented | No enum found. |
| CustomerId | Not Implemented | No Shipment field found. |
| RouteId | Not Implemented | No Shipment field found. |
| VehicleId | Not Implemented | No Shipment field found. |
| Pickup/delivery timestamps | Not Implemented | No estimated/actual pickup or delivery fields found. |
| Notes | Not Implemented | No Shipment notes field found. |
| State-machine transition validation | Partially Implemented | `ChangeStatus` exists but does not enforce full allowed transition map. |
| CreateShipment | Implemented | gRPC maps to command, command persists shipment/cargo/history/outbox, tests pass. |
| GetShipment | Not Implemented | gRPC method throws Unimplemented. |
| ListShipments | Not Implemented | RPC declared; service method not implemented. |
| SubmitShipment | Not Implemented | No RPC/command found. |
| UpdateShipment | Not Implemented | No command/RPC implementation found. |
| UpdateShipmentStatus | Not Implemented | gRPC method throws Unimplemented. |
| GetShipmentTimeline | Not Implemented | RPC declared; service method not implemented. |
| CancelShipment | Not Implemented | RPC declared; service method not implemented. |
| Draft deletion | Not Implemented | No command/RPC found. |
| Cargo update | Not Implemented | Create-only cargo behavior exists. |
| Location management | Not Implemented | No location entity or commands found. |
| Document metadata attachment | Not Implemented | No document entity or commands found. |
| Milestone creation | Partially Implemented | Status history exists; business milestone entity is missing. |
| CSV/Excel import | Not Implemented | No import command/API found. |
| Required integration events | Partially Implemented | ShipmentCreated, ShipmentStatusChanged, ShipmentCancelled contracts exist. Remaining events missing. |
| Outbox publication coverage | Partially Implemented | CreateShipment writes ShipmentCreated outbox record. Worker/publisher not implemented. |
| Integration tests for full MVP | Partially Implemented | 11 tests cover CreateShipment, validation, outbox, tenant isolation only. |

## Summary

CreateShipment vertical slice is implemented and tested. Full Shipment Workflow MVP remains in progress and requires aggregate expansion, state machine, query/command implementation, cargo/location/document/milestone management, import, expanded contracts/events, migrations, and full test coverage.
