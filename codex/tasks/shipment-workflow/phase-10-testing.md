# Phase 10 — Testing

## Status

Completed

## Goal

Verify the first Shipment Workflow vertical slice and its tenant isolation behavior.

## Prerequisites

* Phase 08 — Create Shipment Vertical Slice
* Phase 09 — Database Migration

## Read First

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* `codex/specs/shipment-workflow.md`
* Existing test conventions in the repository

## Scope

Test the CreateShipment flow.

Do not implement unrelated features only to satisfy tests.

## Required Test Scenarios

### Successful Shipment Creation

Given:

```text
Authenticated user
Valid TenantId in current-user context
Valid customer name
Valid destination address
Valid cargo items
```

Expect:

```text
Shipment is created
TenantId comes from authentication
ShipmentNo is generated
Status is Created
Cargo items are stored
Initial history is stored
Outbox message is stored
Response is returned
```

### Missing Tenant Context

Expect:

```text
Request is rejected
No shipment is stored
```

### Invalid Customer Name

Expect:

```text
Validation error
No shipment is stored
```

### Invalid Destination Address

Expect:

```text
Validation error
No shipment is stored
```

### Invalid Cargo Quantity

Input:

```text
Quantity <= 0
```

Expect:

```text
Validation error
No shipment is stored
```

### Invalid Cargo Weight

Input:

```text
WeightKg < 0
```

Expect:

```text
Validation error
No shipment is stored
```

### Tenant Isolation

Given:

```text
Shipment belongs to Tenant A
Current user belongs to Tenant B
```

Expect:

```text
Tenant B cannot access the shipment
```

## Test Types

At least one integration test must verify shipment creation.

Use unit tests for:

* Domain validation
* Status transition rules
* Cargo validation

Use integration tests for:

* DbContext behavior
* Tenant filtering
* Command handling
* gRPC flow when practical

## Manual Validation

The gRPC endpoint may also be tested using:

* Postman gRPC
* grpcurl

## Commands

Use the repository test command or:

```bash
dotnet test
```

For a specific test project:

```bash
dotnet test path/to/test-project.csproj
```

Always run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
```

## Constraints

* Do not disable tenant filtering to make tests pass.
* Do not mock away the core behavior being tested.
* Do not use production credentials.
* Do not modify unrelated services.
* Do not claim coverage that was not executed.

## Completion Criteria

* Shipment Workflow builds successfully.
* Required tests pass.
* At least one integration test covers CreateShipment.
* Tenant isolation is verified.
* Validation failures are verified.
* Test results are recorded.

## Work Log

### Completed

* Added `ShipmentWorkflow.Tests` xUnit test project.
* Added PostgreSQL-backed integration tests for `CreateShipmentCommandHandler`.
* Tested successful shipment creation.
* Tested missing tenant context.
* Tested required customer and destination validation.
* Tested invalid cargo name, quantity, and weight validation.
* Tested initial status history creation.
* Tested outbox record creation.
* Tested tenant isolation for shipments, cargo items, and status histories.
* Fixed ShipmentWorkflow tenant query filters so missing tenant context no longer disables filtering or throws nullable-value errors.
* Ran all Shipment Workflow tests successfully.

### Files Changed

* `src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj`
* `src/dotnet/ShipmentWorkflow/Tests/CreateShipmentCommandHandlerTests.cs`
* `src/dotnet/ShipmentWorkflow/Infrastructure/Persistences/ShipmentWorkflowDbContext.cs`
* `codex/tasks/shipment-workflow/phase-10-testing.md`
* `codex/plan.md`

### Commands Executed

* `git status --short`
* `find . -type f \( -name '*Tests.csproj' -o -name '*Test.csproj' \) -not -path '*/bin/*' -not -path '*/obj/*'`
* `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj`
* `dotnet new xunit -n ShipmentWorkflow.Tests -o src/dotnet/ShipmentWorkflow/Tests --framework net10.0`
* `dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj`
* `git diff --check`

### Build Result

Passed. `dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj` completed with 0 errors and 0 warnings.

### Test Result

Passed. `dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj` completed with 11 tests passed, 0 failed, and 0 warnings.

### Remaining Issues

No Phase 10 test blocker remains. The integration tests require local PostgreSQL on `localhost:5433` with user/password `postgres/postgres`, matching the development Docker Compose setup.

