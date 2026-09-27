# Regulatory Compliance database

The initial EF migration creates the Regulatory Compliance schema on a new PostgreSQL database. Embeddings are stored as `real[]`; this schema does not require the `vector` PostgreSQL extension.

For integration tests, set `AURORA_TEST_POSTGRES_CONNECTION` to an admin connection string for a disposable local PostgreSQL instance, then run `dotnet test Tests/RegulatoryCompliance.Tests.csproj`. The fixture creates and reuses `aurora_regulatory_compliance_tests` and truncates its service tables between tests. One test also requires RabbitMQ at `localhost:5672`.

Before deploying against an existing database, compare its tables and constraints with `20260927022155_InitialRegulatoryCompliance.cs`. EF must not run this initial migration on a schema that already contains those tables. Baseline an existing schema only after confirming that it matches the migration; apply the migration directly only to a new database.
