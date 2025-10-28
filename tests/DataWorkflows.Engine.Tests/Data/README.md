# Data Layer Integration Tests

This directory contains integration tests for the workflow lifecycle repositories that require a PostgreSQL database.

## Running the Tests

These tests are marked with the `RequiresPostgres` trait and will be skipped in CI/local test runs by default.

### Prerequisites

1. **PostgreSQL** must be running and accessible
2. Set the `POSTGRES_CONNECTION_STRING` environment variable (optional, defaults to `Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres`)

### Run Integration Tests

```bash
# Run ONLY the integration tests that require Postgres
dotnet test --filter "Category=RequiresPostgres"

# Run all tests EXCEPT those requiring Postgres (default)
dotnet test --filter "Category!=RequiresPostgres"

# Run all tests (requires Postgres to be running)
dotnet test
```

## Test Coverage

### WorkflowRepositoryTests
Tests for workflow metadata and lifecycle state transitions:
- Creating draft workflows
- Updating draft workflows
- Publishing workflows (Draft → Active)
- Archiving workflows (Active → Archived)
- Reactivating workflows (Archived → Active)
- Deleting draft workflows
- Querying workflows with filters (status, isEnabled)
- State transition validation (e.g., can't update Active workflows, can't delete non-Draft workflows)

### WorkflowDefinitionRepositoryTests
Tests for workflow definition version management:
- Creating/updating draft definitions (version 0)
- Publishing new versions (auto-increment: 1, 2, 3...)
- Checksum-based idempotency (same content = same version)
- Retrieving specific versions
- Retrieving latest version
- Retrieving all versions
- Deleting draft definitions

## Database Isolation

Each test class creates its own isolated PostgreSQL database using `TestDatabase.GetConnectionString()`. The test databases are:
- Named with a `test_dataworkflows_` prefix + GUID
- Created fresh for each test class
- Migrated to the latest schema automatically
- Cleaned up after tests complete

This ensures complete test isolation and prevents flaky tests due to shared state.
