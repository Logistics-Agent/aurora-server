Read all repository instructions and inspect the actual codebase before making changes.

## Current verified state

Shipment Workflow Phases 01–10 have been completed.

Verified results:

* Phase 09 migration applied successfully to the local Shipment Workflow PostgreSQL database.
* Phase 10 added PostgreSQL-backed integration tests.
* Shipment Workflow build passes.
* Shipment Workflow tests pass: 11 passed, 0 failed.
* CreateShipment vertical slice is implemented, migrated, and tested.
* No other service implementation has been started.
* No push has been performed.

Important distinction:

```text
CreateShipment vertical slice: Completed
Full Shipment Workflow MVP: Not yet completed
```

The following gRPC operations are currently declared but do not yet have complete business implementations:

* GetShipment
* ListShipments
* UpdateShipmentStatus
* GetShipmentTimeline
* CancelShipment

The current implementation mainly contains:

* Shipment
* CargoItem
* ShipmentStatusHistory
* OutboxMessage
* CreateShipment flow
* Initial migration
* CreateShipment integration tests

This prompt authorizes:

1. Normalizing and committing the project documentation.
2. Creating the logistics architecture specification.
3. Creating complete specifications and phase plans for every assigned service.
4. Auditing the existing Shipment Workflow implementation against the complete logistics MVP architecture.
5. Creating and implementing the remaining Shipment Workflow MVP phases.
6. Updating all documentation with actual implementation evidence.

This prompt does not authorize starting another service implementation or creating another service branch yet.

---

# 1. Read required repository context

Before editing, read and inspect:

* `AGENTS.md`
* `codex/requirement.md`
* `codex/plan.md`
* All files under `codex/specs/`
* All files under `codex/tasks/`
* All files under `codex/prompts/`
* Shipment Workflow source code
* Shipment contracts
* Shared projects
* Test projects
* Solution and project files
* Existing migrations
* Docker configuration
* Git history
* Current working tree state

Run:

```bash
git status --short
git branch --show-current
git log --oneline --decorate -20
```

Run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
```

Do not claim that the current baseline passes unless these commands actually pass.

Do not:

* Reset
* Clean
* Stash
* Revert
* Discard user changes
* Push
* Merge
* Rebase
* Modify `main`
* Switch to a nonexistent `develop` branch

---

# 2. Handle current untracked files safely

The working tree may contain untracked project documentation or setup files, including:

```text
AGENTS.md
codex/requirement.md
codex/specs/
codex/tasks/
docker-compose.dev.yml
```

Inspect every untracked file before staging it.

Do not automatically assume an untracked file is unrelated.

Classify each file as one of:

* Valid project documentation
* Valid local-development infrastructure
* Generated file
* Unrelated user file
* Obsolete duplicate

Do not delete any file automatically.

Valid documentation files may be included in documentation commits after inspection.

`docker-compose.dev.yml` must not be included in a documentation commit. If it is valid project infrastructure and should be versioned, validate it and commit it separately with an appropriate `chore` commit. Otherwise leave it untouched and report its status.

Never use broad staging commands such as:

```bash
git add .
git add codex
git add src
git add src/dotnet
```

Stage explicit file paths only.

---

# 3. Create the logistics architecture specification

Create or fully update:

```text
codex/specs/logistics-architecture.md
```

This file is the cross-service source of truth.

It must contain the following sections.

## 3.1 Service ownership

Document the complete service ownership map:

### Thanh Tân

* API Gateway & BFF
* Identity & Tenant Service
* Route Planning Agent Service
* AI Ops & Monitoring Service
* Audit Log Service

### Đào Huỳnh

* Financial & Cost Estimation Service
* Billing & Settlement Service
* Realtime Hub
* Negotiation Agent Service
* Customer Assistant

### Ngọc Khoa

* Shipment Workflow Service
* Notification Service
* GPS Tracking & Monitoring Service
* Document OCR Agent Service
* Regulatory Compliance RAG Service

### Hùng Vũ

* Email Agent
* Testing
* Documentation

### Minh Huy

* Frontend

## 3.2 Transportation service boundaries

Clearly distinguish:

### Route Planning Agent

Answers:

```text
Which route should the shipment take?
```

Owns:

* Initial route planning
* Distance and travel-time calculation
* Stops
* Traffic considerations
* Weather considerations
* Weight restrictions
* Alternative routes
* Re-routing

### GPS Tracking and Monitoring

Answers:

```text
Where is the vehicle or shipment now?
```

Owns:

* GPS ingestion
* Position history
* Speed
* Heading
* Timestamp
* Vehicle-to-shipment assignment
* Geofences
* Signal-loss detection
* Abnormal-stop detection
* Realtime tracking publication

GPS must not own:

* Route planning
* Route optimization
* Re-routing
* Shipment business workflow
* Cost estimation
* ETA prediction logic

### Cargo Visibility and ETA

Document that a separate ETA service is not required for the current MVP.

For MVP:

* GPS provides current tracking data.
* Route Planning may contain ETA and delay prediction.
* GPS must not absorb Route Planning responsibilities.

## 3.3 OCR and compliance boundaries

### Document OCR Agent

Owns:

* Document type detection
* OCR
* Layout analysis
* Field extraction
* JSON normalization
* Confidence scoring
* Needs-review flags

OCR does not decide whether cargo complies with regulations.

### Regulatory Compliance RAG

Owns:

* Regulation retrieval
* Import/export restriction checks
* Dangerous-goods checks
* Required-document checks
* Compliance evidence
* Violations
* Missing documents
* Risk level
* Confidence or assumptions

Document this flow:

```text
Document
→ Document OCR Agent
→ Structured JSON
→ Regulatory Compliance RAG
→ Compliance result
```

OCR must not write directly to the Shipment Workflow database.

Compliance must not write directly to the Shipment Workflow database.

## 3.4 Shipment ownership

Document:

```text
Shipment Workflow Service [CORE] — Shipment aggregate owner
```

Shipment Workflow is the single source of truth for:

* Shipment
* Cargo
* Shipment locations
* Shipment document metadata
* Shipment milestones
* Shipment status
* Shipment lifecycle
* Tenant ownership
* Customer ownership

Other services must not directly query or update its database.

Cross-service communication must use:

* Contracts
* APIs
* Integration events
* IDs stored as external references

There must be no cross-service database foreign keys.

## 3.5 Main data flows

Document:

### Client creates Shipment

```text
Frontend
→ API Gateway & BFF
→ Shipment Workflow
→ Shipment database
→ ShipmentCreated event
```

### Staff imports CSV or Excel

```text
Staff
→ API Gateway
→ Shipment Workflow
→ Validate rows
→ Create Shipment and Cargo
→ Publish events
```

For MVP:

* Small files may be processed synchronously.
* Large files should use a background import job.

### Create Shipment from documents

```text
Client or Staff
→ Upload document
→ Document OCR Agent
→ Structured JSON
→ Staff review when confidence is low
→ Shipment Workflow
→ Create or update Shipment
```

### Create Shipment from email

```text
Incoming email
→ Email Agent
→ Attachments
→ Document OCR Agent
→ Structured data
→ Shipment Workflow
```

### Shipment event consumers

Document that Shipment events may be consumed by:

* Route Planning Agent
* Financial & Cost Estimation
* Regulatory Compliance RAG
* Notification Service
* Audit Log
* Customer Assistant
* GPS Tracking when relevant

## 3.6 Full Shipment Workflow responsibilities

Document that Shipment Workflow must support:

* Client-created Shipment
* Staff-created Shipment
* CSV or Excel import
* Shipment update
* Draft deletion
* Shipment submission
* Shipment cancellation
* Shipment list
* Shipment detail
* Tenant ownership
* Customer ownership
* Cargo management
* Location management
* Document metadata management
* Milestone management
* State-machine validation
* Integration-event generation

## 3.7 Shipment state machine

Use the MVP state model:

```text
Draft
→ Submitted
→ Planning
→ Negotiating
→ Confirmed
→ PickedUp
→ InTransit
→ Delivered
→ Completed
```

Also support:

```text
CustomsProcessing
Cancelled
```

Cancellation must only be permitted from explicitly allowed states.

Clients must not assign arbitrary statuses.

All transitions must pass through domain or application validation.

## 3.8 Shipment events

Document the minimum integration events:

* ShipmentCreated
* ShipmentSubmitted
* ShipmentUpdated
* ShipmentCancelled
* ShipmentStatusChanged
* CargoUpdated
* DocumentAttached
* RouteAssigned
* ShipmentPickedUp
* ShipmentDelivered
* ShipmentCompleted

All reliable integration-event publication must use the repository's outbox approach.

## 3.9 Shipment MVP entities

Document exactly five primary MVP entities:

### Shipment

Minimum fields:

* Id
* ShipmentNumber
* TenantId
* CustomerId
* Status
* Priority
* TransportMode
* RouteId
* VehicleId
* EstimatedPickupTime
* EstimatedDeliveryTime
* ActualPickupTime
* ActualDeliveryTime
* Notes
* CreatedBy
* CreatedAt
* UpdatedAt

### Cargo

Minimum fields:

* Id
* ShipmentId
* Name
* Description
* HSCode
* Quantity
* Unit
* WeightKg
* VolumeM3
* DeclaredValue
* Currency
* IsDangerousGoods
* PackageType

The existing class may currently be named `CargoItem`.

Do not rename it automatically merely for naming consistency. Inspect compatibility and migration impact first.

### ShipmentLocation

Minimum fields:

* Id
* ShipmentId
* Type
* Name
* Address
* Latitude
* Longitude
* ContactName
* ContactPhone
* Sequence

### ShipmentDocument

Minimum fields:

* Id
* ShipmentId
* FileName
* DocumentType
* StorageUrl
* OCRStatus
* OCRConfidence
* UploadedBy
* UploadedAt
* ExtractedDataJson

Shipment Workflow stores metadata and references. Actual files should remain in object storage or the repository-defined storage system.

### ShipmentMilestone

Minimum fields:

* Id
* ShipmentId
* Status
* Description
* Latitude
* Longitude
* RecordedAt
* Source
* CreatedBy

GPS owns detailed tracking history.

Shipment Workflow stores business milestones only.

## 3.10 Enums

Document:

* ShipmentStatus
* ShipmentPriority
* TransportMode
* LocationType
* DocumentType
* OCRStatus
* MilestoneSource

Do not modify implemented enums until the existing code, database migration, contracts, and tests have been audited.

## 3.11 Out-of-scope MVP features

Explicitly exclude:

* Billing transactions
* Payment processing
* Detailed GPS history in Shipment Workflow
* Route geometry ownership
* Full carrier management
* Warehouse inventory
* Full customs declaration
* Insurance policy management
* Contract management
* Complex container management
* Generic workflow engines
* Vector databases inside Shipment Workflow

---

# 4. Normalize the Codex documentation structure

Use this structure:

```text
codex/
├── requirement.md
├── plan.md
├── specs/
│   ├── logistics-architecture.md
│   ├── shipment-workflow.md
│   ├── notification.md
│   ├── gps-tracking.md
│   ├── document-ocr.md
│   └── regulatory-compliance-rag.md
│
├── tasks/
│   ├── shipment-workflow/
│   ├── notification/
│   ├── gps-tracking/
│   ├── document-ocr/
│   └── regulatory-compliance-rag/
│
└── prompts/
    ├── shipment-workflow/
    ├── notification/
    ├── gps-tracking/
    ├── document-ocr/
    └── regulatory-compliance-rag/
```

If completed Shipment phase files are directly under `codex/tasks/`, move only confirmed Shipment Workflow files into:

```text
codex/tasks/shipment-workflow/
```

Preserve their content.

Do not recreate completed task files from scratch.

Do not lose their work logs, build evidence, test evidence, migration evidence, or completion status.

Update references in:

* `AGENTS.md`
* `codex/plan.md`
* Shipment prompts
* Any task or spec that points to the old paths

Use `git mv` when moving already tracked files.

For untracked files, move them normally and inspect the resulting diff.

---

# 5. Update AGENTS.md

Ensure the root `AGENTS.md` requires Codex to read:

1. `codex/requirement.md`
2. `codex/plan.md`
3. `codex/specs/logistics-architecture.md`
4. The active service specification
5. The active phase file
6. Relevant source code

Add or retain these mandatory rules:

* Execute only the active service and active phase.
* Inspect before editing.
* Run baseline build.
* Build, test, diagnose, and fix.
* Do not suppress valid tests.
* Update the task file and plan with actual results.
* Never claim success without command evidence.
* Do not use broad Git staging commands.
* Do not push, merge, rebase, reset, or clean unless explicitly instructed.
* Do not access another service's database.
* Do not create cross-service database foreign keys.
* Do not start the next service automatically.
* A new service branch may only be created after the current service is fully completed and committed.

---

# 6. Update requirement.md

Update `codex/requirement.md` so it:

* References `codex/specs/logistics-architecture.md`.
* Identifies Shipment Workflow as the Shipment aggregate owner.
* Lists the assigned services.
* Defines cross-service database isolation.
* Defines event-driven integration.
* Defines tenant isolation requirements.
* Distinguishes current implemented state from full MVP scope.
* Does not falsely state that the full Shipment MVP is already complete.

Do not duplicate the entire architecture file unnecessarily.

Use links or references to the relevant specifications where appropriate.

---

# 7. Create complete service specifications

Create or update:

```text
codex/specs/shipment-workflow.md
codex/specs/notification.md
codex/specs/gps-tracking.md
codex/specs/document-ocr.md
codex/specs/regulatory-compliance-rag.md
```

Each specification must include:

* Purpose
* Boundaries
* Owned data
* Data not owned
* Dependencies
* Contracts
* APIs
* Event consumers
* Event publishers
* Domain model
* Persistence
* Tenant behavior
* Idempotency
* Retry behavior
* Security
* Validation
* Runtime configuration
* Migration requirements
* Test requirements
* Definition of done
* Assumptions
* Explicitly excluded responsibilities

Do not implement future services during this prompt.

Their specifications and task plans may be created, but their production code must remain unchanged.

---

# 8. Create future-service phase plans

Create and fully fill the following future-service task files with status `Not Started`.

Do not create empty placeholders.

## Notification

```text
codex/tasks/notification/phase-01-project-foundation.md
codex/tasks/notification/phase-02-domain-model.md
codex/tasks/notification/phase-03-persistence.md
codex/tasks/notification/phase-04-shipment-event-consumers.md
codex/tasks/notification/phase-05-notification-delivery.md
codex/tasks/notification/phase-06-retry-and-idempotency.md
codex/tasks/notification/phase-07-program-configuration.md
codex/tasks/notification/phase-08-database-migration.md
codex/tasks/notification/phase-09-testing.md
```

Notification must be planned as:

* Independent service
* Own PostgreSQL database
* Shipment event consumer
* No Shipment database access
* Tenant-aware
* Idempotent
* Retry-aware
* Provider abstractions
* Delivery-attempt tracking
* Email and in-app abstraction
* Fake provider support in tests

## GPS Tracking

```text
codex/tasks/gps-tracking/phase-01-project-foundation.md
codex/tasks/gps-tracking/phase-02-contracts-and-domain-model.md
codex/tasks/gps-tracking/phase-03-persistence.md
codex/tasks/gps-tracking/phase-04-location-ingestion.md
codex/tasks/gps-tracking/phase-05-current-location-and-history.md
codex/tasks/gps-tracking/phase-06-shipment-event-consumers.md
codex/tasks/gps-tracking/phase-07-monitoring-rules.md
codex/tasks/gps-tracking/phase-08-realtime-integration.md
codex/tasks/gps-tracking/phase-09-program-and-migration.md
codex/tasks/gps-tracking/phase-10-testing.md
```

GPS must not be planned as Route Planning or ETA ownership.

## Document OCR

```text
codex/tasks/document-ocr/phase-01-project-foundation.md
codex/tasks/document-ocr/phase-02-contracts-and-api.md
codex/tasks/document-ocr/phase-03-document-job-model.md
codex/tasks/document-ocr/phase-04-persistence.md
codex/tasks/document-ocr/phase-05-ocr-provider-abstraction.md
codex/tasks/document-ocr/phase-06-extraction-pipeline.md
codex/tasks/document-ocr/phase-07-retry-and-job-processing.md
codex/tasks/document-ocr/phase-08-program-and-migration.md
codex/tasks/document-ocr/phase-09-testing.md
```

OCR must not be planned as compliance ownership.

## Regulatory Compliance RAG

```text
codex/tasks/regulatory-compliance-rag/phase-01-project-foundation.md
codex/tasks/regulatory-compliance-rag/phase-02-contracts-and-api.md
codex/tasks/regulatory-compliance-rag/phase-03-regulatory-document-model.md
codex/tasks/regulatory-compliance-rag/phase-04-ingestion-and-chunking.md
codex/tasks/regulatory-compliance-rag/phase-05-embedding-and-vector-storage.md
codex/tasks/regulatory-compliance-rag/phase-06-retrieval-and-citations.md
codex/tasks/regulatory-compliance-rag/phase-07-compliance-evaluation.md
codex/tasks/regulatory-compliance-rag/phase-08-program-configuration.md
codex/tasks/regulatory-compliance-rag/phase-09-testing.md
```

Compliance must:

* Return evidence and source references.
* Avoid unsupported conclusions.
* Keep providers behind interfaces.
* Use deterministic fake providers for tests.
* Never require paid AI credentials for automated tests.

Each task file must contain:

```text
# Phase XX — Phase Name

## Status

## Goal

## Prerequisites

## Read First

## Existing State

## Scope

## Required Behavior

## Constraints

## Validation Commands

## Completion Criteria

## Work Log

### Completed

### Files Changed

### Commands Executed

### Build Result

### Test Result

### Runtime Result

### Migration Result

### Remaining Issues
```

---

# 9. Audit the current Shipment Workflow implementation

Compare the current implementation against:

* `codex/specs/logistics-architecture.md`
* `codex/specs/shipment-workflow.md`

Produce a documented gap analysis.

At minimum, inspect whether the implementation contains:

* ShipmentLocation
* ShipmentDocument
* ShipmentMilestone
* ShipmentPriority
* TransportMode
* LocationType
* DocumentType
* OCRStatus
* MilestoneSource
* CustomerId
* RouteId
* VehicleId
* Pickup and delivery timestamps
* Notes
* State-machine transition validation
* GetShipment implementation
* ListShipments implementation
* SubmitShipment implementation
* UpdateShipment implementation
* UpdateShipmentStatus implementation
* GetShipmentTimeline implementation
* CancelShipment implementation
* Draft deletion
* Cargo update
* Location management
* Document metadata attachment
* Milestone creation
* CSV or Excel import
* Required integration events
* Outbox publication coverage
* Integration tests for the complete MVP

Do not assume these features are missing merely because they were not listed in the previous summary.

Inspect the actual source code first.

Record each item as:

```text
Implemented
Partially Implemented
Not Implemented
Out of Scope
Blocked
```

---

# 10. Create remaining Shipment Workflow phase files

Phases 01–10 must remain completed history.

Create additional Shipment Workflow phases beginning from Phase 11.

Use the following minimum plan, but adjust scope only when the repository audit provides a documented reason.

## Phase 11 — Aggregate Expansion

Create:

```text
codex/tasks/shipment-workflow/phase-11-aggregate-expansion.md
```

Cover:

* ShipmentLocation
* ShipmentDocument
* ShipmentMilestone
* Missing Shipment aggregate fields
* Required enums
* Entity relationships
* Tenant ownership
* Domain invariants
* Persistence mappings
* Initial compatibility analysis

## Phase 12 — Workflow State Machine

Create:

```text
codex/tasks/shipment-workflow/phase-12-workflow-state-machine.md
```

Cover:

* Complete Shipment statuses
* Allowed transitions
* Submit
* Planning
* Negotiating
* Confirmed
* PickedUp
* InTransit
* CustomsProcessing
* Delivered
* Completed
* Cancelled
* Transition validation
* Domain events
* Milestone creation from business transitions

## Phase 13 — Shipment Queries

Create:

```text
codex/tasks/shipment-workflow/phase-13-shipment-queries.md
```

Cover:

* GetShipment
* ListShipments
* Tenant filtering
* Pagination
* Safe filtering
* Response mapping
* Cargo
* Locations
* Documents
* Milestones where required

## Phase 14 — Shipment Commands

Create:

```text
codex/tasks/shipment-workflow/phase-14-shipment-commands.md
```

Cover:

* SubmitShipment
* UpdateShipment
* UpdateShipmentStatus
* CancelShipment
* Draft deletion
* Concurrency and validation
* Outbox events

Do not permit clients to assign arbitrary states.

## Phase 15 — Cargo and Location Management

Create:

```text
codex/tasks/shipment-workflow/phase-15-cargo-and-location-management.md
```

Cover:

* Cargo create/update/remove rules
* Location create/update/remove rules
* Location sequence validation
* Pickup and delivery requirements
* CargoUpdated event
* Tenant isolation

## Phase 16 — Document and Milestone Management

Create:

```text
codex/tasks/shipment-workflow/phase-16-document-and-milestone-management.md
```

Cover:

* Document metadata attachment
* OCR status and confidence metadata
* Extracted JSON metadata
* DocumentAttached event
* Business milestone recording
* GPS/system/user milestone sources
* Timeline query completion

Do not implement object storage ownership unless the repository explicitly assigns it to Shipment Workflow.

## Phase 17 — Shipment Import

Create:

```text
codex/tasks/shipment-workflow/phase-17-shipment-import.md
```

Cover:

* Staff CSV or Excel import
* Row-level validation
* Import result reporting
* Small-file synchronous MVP
* Clear limit before background processing is required
* Tenant isolation
* Idempotency where an import request identifier exists

Do not introduce a complex background-import platform unless necessary for the MVP.

## Phase 18 — Contracts and Integration Events

Create:

```text
codex/tasks/shipment-workflow/phase-18-contracts-and-integration-events.md
```

Cover:

* Contract compatibility
* ShipmentCreated
* ShipmentSubmitted
* ShipmentUpdated
* ShipmentCancelled
* ShipmentStatusChanged
* CargoUpdated
* DocumentAttached
* RouteAssigned
* ShipmentPickedUp
* ShipmentDelivered
* ShipmentCompleted
* Outbox serialization
* Event versioning
* Consumer-safe schemas

Do not break existing consumers unnecessarily.

## Phase 19 — Migration and Full MVP Testing

Create:

```text
codex/tasks/shipment-workflow/phase-19-migration-and-full-mvp-testing.md
```

Cover:

* Migration for the expanded aggregate
* Existing-data compatibility
* Database validation
* Domain tests
* Command tests
* Query tests
* Tenant isolation
* State-machine tests
* Cancellation tests
* Timeline tests
* Document tests
* Cargo and location tests
* Import tests
* Outbox-event tests
* Full regression of CreateShipment
* Runtime smoke validation

If the audit shows that a phase should be split, create additional phases with clear justification.

Do not reduce the required scope without documenting why.

---

# 11. Update plan.md accurately

Update:

```text
codex/plan.md
```

The plan must state:

```text
Shipment Workflow CreateShipment vertical slice: Completed
Shipment Workflow full MVP: In Progress
Notification Service: Not Started
GPS Tracking and Monitoring: Not Started
Document OCR Agent: Not Started
Regulatory Compliance RAG: Not Started
```

Set:

```text
Active Service: Shipment Workflow
Active Phase: Phase 11 — Aggregate Expansion
```

Or set the first actual new phase chosen after the audit.

The plan must include:

* Active Service
* Active Phase
* Current Branch
* Service Progress
* Shipment Phase Progress
* Future Service Phase Progress
* Completed Work
* Current Work
* Blocked Work
* Remaining Work
* Build Results
* Test Results
* Migration Results
* Commit History
* Immediate Next Action
* Branch Strategy

Record the future branch strategy:

```text
current Shipment branch
└── feat/notification-service
    └── feat/gps-tracking
        └── feat/document-ocr-agent
            └── feat/regulatory-compliance-rag
```

Do not create these branches yet.

---

# 12. Documentation commits

After the documentation structure, architecture, specifications, task plans, AGENTS rules, and plan are complete:

Run:

```bash
git status --short
git diff
git diff --check
```

Stage only explicit documentation files.

Inspect:

```bash
git diff --cached
```

Create logically separated commits.

Recommended commits:

```text
docs(architecture): define logistics service boundaries
```

and:

```text
docs(shipment): organize phases and define remaining mvp
```

Future service specs and plans may be included in a separate commit:

```text
docs(services): define remaining service plans
```

Do not mix production code into documentation commits.

Do not include `docker-compose.dev.yml` in a docs commit.

Do not push.

---

# 13. Implement remaining Shipment Workflow phases

After the documentation commits succeed, implement only Shipment Workflow Phases 11 onward.

Execute them sequentially.

For each phase:

1. Read:

   * `AGENTS.md`
   * `codex/requirement.md`
   * `codex/plan.md`
   * `codex/specs/logistics-architecture.md`
   * `codex/specs/shipment-workflow.md`
   * The active phase file
2. Inspect existing code.
3. Run baseline build.
4. Run relevant tests.
5. Implement only the active phase.
6. Build again.
7. Diagnose the earliest root-cause error.
8. Fix it.
9. Repeat until the build passes or a genuine blocker exists.
10. Run focused tests.
11. Run all Shipment Workflow tests.
12. Update the phase Work Log with actual evidence.
13. Update `codex/plan.md`.
14. Inspect the complete diff.
15. Stage explicit files only.
16. Inspect the staged diff.
17. Create one local commit for the completed phase.
18. Do not push.
19. Continue only when the phase completion criteria pass.

Run, at minimum:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
git diff --check
```

Do not weaken or delete the existing 11 passing tests.

Add tests for every newly implemented behavior.

---

# 14. Migration safety

Before generating another migration:

1. Inspect the existing migration directory.
2. Run the migration-list command.
3. Inspect the current database schema.
4. Confirm the Shipment Workflow connection string.
5. Confirm the database name.
6. Generate only the required incremental migration.
7. Do not generate another initial migration.
8. Do not delete the applied initial migration.
9. Do not manually alter `__EFMigrationsHistory`.
10. Apply only to the confirmed Shipment Workflow database.

If the current local schema is incompatible with the new model, stop and report before performing destructive operations.

Never drop or reset the database automatically.

---

# 15. Branch rules

Remain on the current Shipment Workflow branch while completing the full Shipment MVP.

Do not rename the current branch automatically.

Do not create:

```text
feat/notification-service
```

until all Shipment Workflow MVP phases are completed, committed, built, migrated, and tested.

When the full Shipment Workflow MVP is complete, stop and report that the next allowed branch is:

```text
feat/notification-service
```

The future branch relationship is intentionally stacked because there is currently no `develop` branch.

Do not create all branches in advance.

---

# 16. Blocking rules

When blocked:

1. Identify the exact blocker.
2. Record the exact command.
3. Record the complete relevant error.
4. Mark the active phase `Blocked`.
5. Update `codex/plan.md`.
6. Do not mark the phase completed.
7. Do not continue to a dependent phase.
8. Do not begin another service.
9. Do not create another branch.
10. Stop and report.

Do not fabricate:

* Credentials
* External systems
* Database success
* Test success
* Runtime success
* Migration success

---

# 17. Final Shipment MVP validation

After all remaining Shipment Workflow phases complete, run:

```bash
dotnet build src/dotnet/ShipmentWorkflow/ShipmentWorkflow.csproj
dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj
git diff --check
git status --short
git log --oneline --decorate -30
```

Validate the Shipment Workflow database and migration list.

Verify:

* Five MVP entities are implemented.
* Tenant isolation remains enforced.
* State transitions are validated.
* Clients cannot set arbitrary statuses.
* GetShipment works.
* ListShipments works.
* SubmitShipment works.
* UpdateShipment works.
* UpdateShipmentStatus works.
* Timeline works.
* CancelShipment works.
* Draft deletion works when permitted.
* Cargo management works.
* Location management works.
* Document metadata works.
* Milestones work.
* Import MVP works.
* Required outbox events are created.
* Existing CreateShipment behavior still works.
* Relevant tests pass.
* Specifications match the code.
* Phase files contain real command evidence.
* `codex/plan.md` matches reality.
* No unrelated changes were committed.
* No secrets were committed.
* No push occurred.
* No next-service branch was created.

---

# 18. Final report

Report:

## Documentation Result

* Architecture file created
* Specs created or updated
* Task structure created or normalized
* AGENTS changes
* Requirement changes
* Plan changes
* Documentation commits

## Shipment Gap Analysis

For every required capability:

* Implemented
* Partially implemented
* Not implemented
* Completed during this run
* Remaining blocker

## Shipment Phase Results

For each new phase:

* Status
* Work completed
* Files changed
* Commands executed
* Build result
* Test result
* Runtime result
* Migration result
* Commit hash
* Remaining issues

## Final Shipment Workflow Status

State separately:

```text
CreateShipment vertical slice status
Full Shipment Workflow MVP status
```

## Git Status

Report:

* Current branch
* Working-tree status
* Local commits
* Untracked files
* Whether any push occurred

## Next Allowed Action

When the full Shipment Workflow MVP is complete, state:

```text
The next allowed service branch is feat/notification-service.
```

Do not begin Notification Service.

Do not create its branch.

Do not push.

Production code changes are allowed only inside services assigned to Ngọc Khoa:

- Shipment Workflow Service
- Notification Service
- GPS Tracking & Monitoring Service
- Document OCR Agent Service
- Regulatory Compliance RAG Service

During this run, production implementation is restricted to Shipment Workflow Service only.

Other teams' services may be inspected only to understand contracts and repository conventions. Do not modify their source code, configuration, migrations, tests, or documentation unless an explicitly required shared contract change is proven necessary. Stop and report before making such a cross-team change.