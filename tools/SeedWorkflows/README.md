# Workflow Seeder Tool

This tool loads workflow JSON fixture files into the database by calling the Workflow API endpoints. It creates Draft workflows and publishes them as Active workflows.

## Usage

```bash
dotnet run --project tools/SeedWorkflows -- <path-to-workflow.json> [api-base-url]
```

### Parameters

- `path-to-workflow.json`: Path to a single workflow JSON file or directory containing workflows
- `api-base-url`: Optional API base URL (default: `http://localhost:5000`)

### Examples

**Seed a single workflow:**
```bash
dotnet run --project tools/SeedWorkflows -- fixtures/bundle1/simple-echo-workflow.json
```

**Seed all workflows in a directory:**
```bash
dotnet run --project tools/SeedWorkflows -- fixtures/bundle1
```

**Seed all workflows in all bundle directories:**
```bash
dotnet run --project tools/SeedWorkflows -- fixtures
```

**Use custom API URL:**
```bash
dotnet run --project tools/SeedWorkflows -- fixtures http://localhost:8080
```

## How It Works

For each workflow file:

1. **Create/Update Draft** - Calls `POST /api/v1/workflows` with the workflow JSON
2. **Publish** - Calls `POST /api/v1/workflows/{id}/publish?autoActivate=true` to create an immutable version and activate it

The tool will:
- Skip files named `execute-request.json` (these are test request payloads, not workflows)
- Display progress and validation warnings
- Report errors if workflows fail to parse or publish

## Prerequisites

- The DataWorkflows.Engine.Api must be running
- PostgreSQL database must be accessible
- All actions referenced in workflows must be registered in the ActionRegistry

## Idempotency

The publish operation is idempotent (uses checksum-based deduplication). Running the seeder multiple times with the same workflow files will:
- Reuse existing versions if the definition hasn't changed
- Create new versions if the definition has changed
