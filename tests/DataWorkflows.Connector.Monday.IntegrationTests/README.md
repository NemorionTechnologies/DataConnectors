# Monday.com Connector Integration Tests

This directory contains integration tests for the Monday.com connector that make real API calls.

## Setup

### Option 1: Using testsettings.json (Recommended)

1. Copy `testsettings.example.json` to `testsettings.json`:
   ```bash
   cp testsettings.example.json testsettings.json
   ```

2. Update `testsettings.json` with your actual values:
   ```json
   {
     "Monday": {
       "BoardId": "12345678",
       "StatusColumnId": "status",
       "StatusLabel": "Working on it",
       "TestItemId": "",
       "ApiKey": "eyJhbGciOiJIUzI1NiJ9..."
     }
   }
   ```

3. **Important**: `testsettings.json` is in `.gitignore` and will NOT be committed to the repository.

### Option 2: Using Environment Variables

Set the following environment variables:

```bash
# Windows PowerShell
$env:MONDAY_BOARD_ID="12345678"
$env:MONDAY_API_TOKEN="eyJhbGciOiJIUzI1NiJ9..."
$env:MONDAY_STATUS_COLUMN_ID="status"
$env:MONDAY_STATUS_LABEL="Working on it"

# Windows CMD
set MONDAY_BOARD_ID=12345678
set MONDAY_API_TOKEN=eyJhbGciOiJIUzI1NiJ9...
set MONDAY_STATUS_COLUMN_ID=status
set MONDAY_STATUS_LABEL=Working on it

# Linux/Mac
export MONDAY_BOARD_ID="12345678"
export MONDAY_API_TOKEN="eyJhbGciOiJIUzI1NiJ9..."
export MONDAY_STATUS_COLUMN_ID="status"
export MONDAY_STATUS_LABEL="Working on it"
```

## Configuration Values

### BoardId
Your Monday.com board ID. Find it in the URL:
```
https://[your-domain].monday.com/boards/12345678
                                         ^^^^^^^^^
```

### ApiKey
Your Monday.com API token. Generate one at:
https://[your-domain].monday.com/admin/integrations/api

### StatusColumnId
The ID of a status column on your board. Run the `DiscoverBoardStructure` test to find available column IDs.

### StatusLabel
A valid status label for your status column (e.g., "Working on it", "Done", "Stuck").

## Running Tests

### Discovery Tests (Run These First)

These tests help you understand your board structure:

```bash
# Run discovery test
dotnet test --filter "FullyQualifiedName~BoardDiscoveryTests.DiscoverBoardStructure"

# Or test API connection
dotnet test --filter "FullyQualifiedName~TestApiConnection"
```

**Important**: Remove the `Skip` parameter from the test attribute before running.

### Real API Tests

After configuring your board settings, you can run the real API tests:

```bash
# Run all integration tests (skipped by default)
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~GetBoardItems"
```

**Important**: Remove the `Skip` parameter from tests you want to run.

## Board Requirements

Your Monday.com board should have:
- At least 1 group
- At least 1 item in that group
- A status column (for write tests)
- Optional: sub-items, updates/comments

## Warning

Some tests modify your board (e.g., `UpdateColumnValue_ShouldUpdateStatusColumn`). These are clearly marked with warnings in the test documentation.

## Test Configuration Priority

The configuration is loaded in this order (later values override earlier ones):
1. `testsettings.json`
2. `testsettings.Development.json`
3. Environment variables

Environment variables use the `${VAR_NAME}` syntax in JSON files.
