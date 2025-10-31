# Bundle 5: Monday Connector Integration - Progress Report

## Summary

This document tracks the progress of Bundle 5 implementation, which integrates the Monday connector with the Workflow Engine through a clean plugin architecture.

## Completed Work (Engine Side)

### 1. Database Layer ✅
**Files Created:**
- `src/DataWorkflows.Data/Migrations/008_CreateActionCatalog.sql` - Migration to create ActionCatalog table
- `src/DataWorkflows.Data/Models/ActionCatalogEntry.cs` - Entity model for action catalog entries
- `src/DataWorkflows.Data/Repositories/IActionCatalogRepository.cs` - Repository interface
- `src/DataWorkflows.Data/Repositories/ActionCatalogRepository.cs` - PostgreSQL repository implementation

**Features:**
- ActionCatalog table with columns: Id, ActionType, ConnectorId, DisplayName, Description, ParameterSchema (JSONB), OutputSchema (JSONB), IsEnabled, RequiresAuth, CreatedAt, UpdatedAt
- Unique constraint on (ConnectorId, ActionType) for idempotent upsert capability
- Indexes for fast lookups by ActionType, ConnectorId, and IsEnabled
- Repository methods for upsert, lookup, and querying actions

### 2. Validation Layer ✅
**Files Created:**
- `src/DataWorkflows.Engine.Core/Validation/ISchemaValidator.cs` - Schema validator interface
- `src/DataWorkflows.Engine.Core/Validation/SchemaValidator.cs` - NJsonSchema-based implementation

**Features:**
- JSON Schema (draft 2020-12) validation for action parameters
- Validates both JSON strings and Dictionary<string, object?> parameters
- Returns detailed validation errors with paths

### 3. ActionCatalog Registry ✅
**Files Created:**
- `src/DataWorkflows.Engine.Core/Services/IActionCatalogRegistry.cs` - Registry interface
- `src/DataWorkflows.Engine.Core/Services/ActionCatalogRegistry.cs` - Thread-safe in-memory cache implementation
- `src/DataWorkflows.Engine.Api/Services/ActionCatalogRegistryInitializer.cs` - Background service for initialization and periodic refresh

**Features:**
- In-memory cache of ActionCatalog entries for fast lookup
- Loads from database on startup
- Refreshes every 5 minutes automatically
- Manual refresh endpoint available

### 4. Admin API ✅
**Files Created:**
- `src/DataWorkflows.Engine.Api/Controllers/AdminController.cs` - Admin API controller

**Endpoints:**
- `POST /api/v1/admin/actions/register` - Register actions from a connector (idempotent)
- `POST /api/v1/admin/actions/refresh` - Manually refresh ActionCatalogRegistry cache
- `GET /api/v1/admin/actions/registry` - Get registry information

**Features:**
- Validates that action types match connector ID convention (e.g., "monday.get-items")
- Upserts actions in database
- Auto-refreshes registry cache after registration
- Returns detailed registration summary

### 5. Configuration ✅
**Files Updated:**
- `src/DataWorkflows.Engine.Core/DataWorkflows.Engine.Core.csproj` - Added NJsonSchema package
- `src/DataWorkflows.Engine.Api/Program.cs` - Registered all new services
- `src/DataWorkflows.Engine.Api/appsettings.json` - Updated PostgreSQL port to 5433

### 6. Verification ✅
**Status:**
- ✅ Database migration applied successfully
- ✅ ActionCatalog table created with correct schema
- ✅ Engine starts successfully
- ✅ ActionCatalogRegistry initializes with 0 actions
- ✅ Admin API endpoints respond correctly
- ✅ Solution builds without errors

## Completed Work (Monday Connector Side) ✅

### 1. Add NJsonSchema Package ✅
**Files Updated:**
- `src/DataWorkflows.Connector.Monday/DataWorkflows.Connector.Monday.csproj` - Added NJsonSchema 11.* package

### 2. Create GetItems Action Implementation ✅
**Files Created:**
- `src/DataWorkflows.Connector.Monday/Actions/Models/GetItemsParameters.cs` - Parameter POCO with BoardId and Filter
- `src/DataWorkflows.Connector.Monday/Actions/Models/GetItemsOutput.cs` - Output POCO with Items, Count, BoardId
- `src/DataWorkflows.Connector.Monday/Actions/MondayGetItemsAction.cs` - IWorkflowAction implementation

**Features:**
- Implements IWorkflowAction with Type = "monday.get-items"
- Deserializes parameters from Dictionary<string, object?> to typed GetItemsParameters
- Calls existing MediatR query (GetBoardItemsQuery) to leverage existing Monday API infrastructure
- Maps MondayItemDto to workflow output format (MondayItem)
- Returns ActionExecutionResult with Succeeded/Failed/RetriableFailure status
- Intelligent error handling with retry detection for transient errors

### 3. Create Generic Execute Endpoint ✅
**Files Created:**
- `src/DataWorkflows.Connector.Monday/Controllers/ActionsController.cs`

**Features:**
- POST /api/v1/actions/execute endpoint
- Accepts ActionExecutionRequest (ActionType, Parameters, ExecutionContext)
- Resolves IWorkflowAction by type from DI container
- Creates ActionExecutionContext with WorkflowExecutionId, NodeId, Parameters, Services
- Returns ActionExecutionResultDto with Status, Outputs, Error
- Always returns 200 OK with status in body (even for failures)

### 4. Create Action Registration Service ✅
**Files Created:**
- `src/DataWorkflows.Connector.Monday/HostedServices/ActionRegistrationService.cs`
- `src/DataWorkflows.Connector.Monday/Actions/Schemas/SchemaGenerator.cs`

**Features:**
- Background service implementing BackgroundService
- Reads WORKFLOW_ENGINE_URL from environment variable (fallback to config, default: http://localhost:5131)
- Generates JSON Schemas using NJsonSchema with SystemTextJsonSchemaGeneratorSettings
- Registers monday.get-items action with engine on startup
- Retry logic: 10 attempts with exponential backoff (2-60 seconds)
- Detailed logging of registration attempts and responses
- Successfully tested and verified with engine

### 5. Update DI Configuration ✅
**Files Updated:**
- `src/DataWorkflows.Connector.Monday/Program.cs`

**Changes:**
- Registered IWorkflowAction implementation: MondayGetItemsAction (scoped)
- Registered ActionRegistrationService as hosted service
- Added using statements for Actions and HostedServices namespaces
- Connector now auto-registers with engine on startup

### 6. Verification ✅
**Status:**
- ✅ Monday connector builds successfully
- ✅ ActionRegistrationService starts and registers actions
- ✅ JSON Schemas generated correctly (including complex filter schemas)
- ✅ Action registered in ActionCatalog table: monday.get-items
- ✅ Action visible in ActionCatalogRegistry via admin API
- ✅ Engine shows "1 action from 1 connector" in registry
- ✅ ActionsController ready to accept execution requests

**Registration Response:**
```json
{
  "message": "Successfully registered 1 actions from connector 'monday'.",
  "connectorId": "monday",
  "actionsRegistered": 1,
  "actionTypes": ["monday.get-items"],
  "timestamp": "2025-10-28T23:51:49.5150985Z"
}
```

## Completed Work (Remote Action Execution) ✅

### 1. Implement RemoteActionExecutor ✅
**Files Created:**
- `src/DataWorkflows.Engine.Core/Services/IRemoteActionExecutor.cs` - Interface for remote action execution
- `src/DataWorkflows.Engine.Core/Services/RemoteActionExecutor.cs` - HTTP client implementation

**Files Updated:**
- `src/DataWorkflows.Engine.Core/Application/Orchestration/WorkflowConductor.cs` - Added remote action detection and execution
- `src/DataWorkflows.Engine.Core/Domain/Validation/WorkflowValidator.cs` - Added ActionCatalogRegistry for remote action validation
- `src/DataWorkflows.Engine.Api/Controllers/WorkflowsController.cs` - Injected ActionCatalogRegistry for validation
- `src/DataWorkflows.Engine.Api/Program.cs` - Registered RemoteActionExecutor in DI container
- `src/DataWorkflows.Engine.Api/appsettings.json` - Added Connectors configuration section
- `src/DataWorkflows.Engine.Core/DataWorkflows.Engine.Core.csproj` - Added Microsoft.Extensions.Http package

**Features:**
- RemoteActionExecutor service with IHttpClientFactory integration
- Connector URL lookup from configuration (environment variable or appsettings.json)
- POST to `{connectorUrl}/api/v1/actions/execute` with ActionExecutionContext
- HTTP timeout and error handling (maps to RetriableFailure)
- WorkflowConductor detects local vs remote actions using ActionCatalogRegistry
- WorkflowValidator checks both ActionRegistry (local) and ActionCatalogRegistry (remote) for action availability
- Proper integration into existing retry and timeout logic

**Verification:**
- ✅ Workflow published successfully with monday.get-items remote action
- ✅ Workflow executed with HTTP call to Monday connector at http://localhost:5192
- ✅ RemoteActionExecutor logged execution details
- ✅ Monday connector received request and processed action
- ✅ Complete HTTP round-trip verified in logs (215ms response time)
- ✅ RetriableFailure status correctly returned (expected - no Monday API key)

**Test Execution Log:**
```
Engine API:
  - Action monday.get-items is a remote action from connector monday
  - Executing remote action monday.get-items on connector monday at http://localhost:5192/api/v1/actions/execute
  - Received HTTP response headers after 215.5095ms - 200
  - Remote action monday.get-items completed with status RetriableFailure

Monday Connector:
  - Executing action monday.get-items for workflow 802d04d0-ee29-465a-8106-7e30cf32563a
  - Getting items for board 1234567890
  - GraphQL query executed to https://api.monday.com/v2/
  - Received 401 Unauthorized (expected - no API key)
  - Returned RetriableFailure status to engine
```

**Configuration:**
```json
{
  "Connectors": {
    "monday": {
      "Url": "http://localhost:5192"
    }
  }
}
```

## Completed Work (Test Fixture) ✅

### 1. Create monday-get-items-workflow.json ✅
**Files Created:**
- `fixtures/bundle5/monday-get-items-workflow.json` - Test workflow using monday.get-items action

**Features:**
- Simple workflow with single node calling monday.get-items
- Uses Scriban templating for boardId parameter ({{ trigger.boardId }})
- Successfully validated and published to workflow engine
- Successfully executed end-to-end (HTTP call to Monday connector verified)

## Completed Work (Schema Validation) ✅

### 1. Execution-Time Parameter Validation ✅
**Files Created:**
- `src/DataWorkflows.Engine.Core/Domain/Validation/ActionCatalogParameterValidator.cs` - Parameter validator implementation

**Files Updated:**
- `src/DataWorkflows.Engine.Api/Program.cs` - Registered ActionCatalogParameterValidator in DI container (replaced NoopParameterValidator)

**Features:**
- Validates action parameters against JSON schemas stored in ActionCatalog
- Looks up action type in ActionCatalogRegistry
- Uses SchemaValidator to validate parameters against ParameterSchemaJson
- Validation occurs after Scriban template rendering in WorkflowConductor:418-422
- Returns detailed validation errors with parameter paths
- Gracefully handles local actions without schemas (returns success)

**Verification:**
- ✅ ActionCatalogParameterValidator created with proper dependencies
- ✅ Registered in DI container as IParameterValidator
- ✅ Solution builds successfully without errors
- ✅ Ready for end-to-end testing

**Implementation Details:**
```csharp
// Validation flow:
1. WorkflowConductor renders parameters with Scriban templates
2. Calls _parameterValidator.Validate(actionType, renderedParameters)
3. ActionCatalogParameterValidator looks up action in ActionCatalogRegistry
4. If found with ParameterSchemaJson, validates using SchemaValidator.ValidateParameters()
5. Returns ParameterValidationResult with detailed errors if validation fails
6. WorkflowConductor throws InvalidOperationException on validation failure
```

## Remaining Work

### 1. Implement Additional Monday Actions
**Task:** Follow the GetItems pattern for remaining actions

**Files to Create (in `src/DataWorkflows.Connector.Monday/Actions/`):**
1. `MondayGetBoardAction.cs` - monday.get-board
2. `MondayGetUpdatesAction.cs` - monday.get-updates
3. `MondayGetActionHistoryAction.cs` - monday.get-actionhistory
4. `MondayGetSubItemsAction.cs` - monday.get-subitems
5. `MondayCreateItemAction.cs` - monday.create-item
6. `MondayCreateSubItemAction.cs` - monday.create-subitem
7. `MondayUpdateColumnAction.cs` - monday.update-column (+ variations for timeline, status, link, text columns)
8. `MondayUpdateSubItemColumnAction.cs` - monday.update-subitem-column (+ variations)

**Note: Create Item and Create SubItem are DIFFERENT OPERATIONS for Monday**
1. Separate GraphQL Mutations:
  - create_item - creates regular items on a board
  - create_subitem - creates sub-items under a parent item
2. Different Required Parameters:
  - create_item requires: board_id, item_name
  - create_subitem requires: parent_item_id, item_name
3. Infrastructure Implications:
  - Need TWO separate methods in IMondayApiClient:
    - CreateItemAsync(boardId, itemName, groupId?, columnValues?)
    - CreateSubItemAsync(parentItemId, itemName, columnValues?)
  - Need TWO separate MediatR commands:
    - CreateItemCommand
    - CreateSubItemCommand
  - Need TWO separate handlers
  - Need TWO separate workflow actions (NOT cosmetic aliases this time)

**Pattern Established:**
- Create parameter/output POCOs in Actions/Models/
- Implement IWorkflowAction with Type property
- Use MediatR to call existing infrastructure
- Map DTOs to output format
- Handle errors with retriable detection
- Update ActionRegistrationService to include new action

### 2. Create Additional Workflow Fixtures
**Task:** Create more example workflows for Bundle 5 testing

**Files to Create (in `fixtures/bundle5/`):**
- `monday-get-items-execute-request.json` - Test request for get-items
- `monday-create-update-workflow.json` - Create item + update column
- `monday-complex-filter-workflow.json` - Uses complex filters from MondayFilterDefinitionFixtures

### 3. Create Integration Tests
**Task:** End-to-end tests for connector registration and execution

**Files to Create:**
- `tests/DataWorkflows.Engine.Tests/TestDoubles/TestConnectorServer.cs` - Mock connector
- `tests/DataWorkflows.Engine.Tests/Integration/MondayConnectorIntegrationTests.cs` - E2E tests

**Test Scenarios:**
- Action registration pathway
- Workflow validation against registered actions
- Action execution via HTTP
- Error scenarios: RetriableFailure, Failed statuses
- Parameter validation failures
- Connector unavailable scenarios

## Architecture Notes

### Distinct Registries
The system now has TWO action registries:
1. **ActionCatalogRegistry (new)** - Holds ActionCatalogEntry metadata for validation (both local and remote actions)
2. **ActionRegistry (existing)** - Holds IWorkflowAction instances for local action execution (core.echo, etc.)

Remote actions (Monday connector) are registered in ActionCatalogRegistry for validation, but executed via HTTP calls.

### Execution Flow
1. User publishes workflow
2. Engine validates action types against ActionCatalogRegistry
3. Engine validates parameters against ParameterSchema (JSON Schema)
4. At execution time:
   - Engine validates concrete parameters again (after templating)
   - If action is remote: POST to connector's `/api/v1/actions/execute`
   - If action is local: Call IWorkflowAction.ExecuteAsync
   - Store result in ActionExecutions table
   - Add outputs to workflow databag (context.data[nodeId])

### Parameter Translation
- Engine passes raw parameters dictionary to connector
- Connector is responsible for translating to Monday DTOs
- Example: Workflow JSON filters → MondayFilterDefinition → Monday API GraphQL

## Next Steps

**Immediate Priority:**
1. Start with `MondayGetItemsAction` to establish the pattern
2. Create schema generation helper to reduce boilerplate
3. Test with simple workflow fixture
4. Incrementally add remaining actions

**Reference Materials:**
- `docs/clarifying_questions.md` - All architectural decisions
- `tests/DataWorkflows.Connector.Monday.IntegrationTests/MondayFilterDefinitionFixtures.cs` - Complex filter examples
- Existing Monday connector infrastructure (IMondayApiClient, IItemFilterService, etc.)

## Current Branch
`feature/EngineIntegration_Monday`

## Database Connection
PostgreSQL: localhost:5433 (Docker container: dataworkflows-postgres)

## Engine Status
RUN THE PROJECT USING DOCKER, DO NOT RUN LOCAL INSTANCES OF THE ENGINE, NOR CONNECTORS - EVERYTHING IS CONTAINERIZED FOR A REASON, USE DOCKER!
