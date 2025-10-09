# DataWorkflows Engine — Implementation Plan v2 (Vertical Slices)

> **Methodology**: Vertical Slice / Feature-First
>
> **Goal**: Deliver working features incrementally. Each bundle produces a deployable system with more capabilities.
>
> **Spec Reference**: `workflow_engine_spec.md` (authoritative source)
>
> **Detailed Reference**: `engine_implementation_plan.md` (160-step breakdown for each bundle)

---

## Key Differences from v1

**v1 (160 steps):**
- Granular: One small task per step
- Best for: Learning, debugging, understanding details
- Time: 12-26 hours (5-10 min/step)
- Progress: Very visible (54% = Step 87/160)

**v2 (17 bundles):**
- Feature-based: Complete working features per bundle
- Best for: Speed, coherent code, deploying quickly
- Time: 8-12 hours (30-60 min/bundle)
- Progress: Feature-complete milestones

**Use v2 for implementation, v1 as detailed reference when debugging.**

---

## LLM Consumption Guide

**This plan is optimized for feeding to LLMs:**

1. **Section Anchors**: References use `§X.Y Section-Name | Lines N-M (approx)` format
2. **Bundle Headers**: Each bundle has Inputs/Implementation/Checkpoint fields
3. **Detailed Steps Reference**: "See v1 Steps X-Y for detailed breakdown"
4. **Fixtures**: Canonical test payloads in `fixtures/` directory
5. **Auth Mode**: Phases 1-16 use `AllowLooseAuth=true`, Bundle 17 flips to strict

---

## Vertical Slice Philosophy

**After each bundle, you have a WORKING, DEPLOYABLE system:**

```
Bundle 1: ✅ Can execute simple workflows via API
Bundle 2: ✅ Bundle 1 + retry logic
Bundle 3: ✅ Bundle 2 + branching/conditions
Bundle 4: ✅ Bundle 3 + templating
...
Bundle 17: ✅ Production-ready workflow engine
```

**Not like this (horizontal layers):**
```
❌ Database layer (can't use it)
❌ Repository layer (can't use it)
❌ API layer (finally works!)
```

---

## Bundle Overview

| Bundle | Feature | Original Steps | Time | Auth Mode |
|--------|---------|----------------|------|-----------|
| 1 | Minimal E2E Workflow | 1-30 | 2-3h | Loose |
| 2 | Retry & Error Handling | 31-40 | 1h | Loose |
| 3 | Branching & Conditions | 41-50 | 1-2h | Loose |
| 4 | Parameter Templating | 51-60 | 1h | Loose |
| 5 | Monday Connector | 61-70 | 1-2h | Loose |
| 6 | Slack Connector | 71-75 | 30m | Loose |
| 7 | Workflow Lifecycle | 76-85 | 1h | Loose |
| 8 | Principal Tracking | 86-92 | 30m | Loose |
| 9 | Resource Links & Idempotency | 93-100 | 1h | Loose |
| 10 | Subworkflows | 101-108 | 1h | Loose |
| 11 | Webhook Triggers | 109-113 | 1h | Loose |
| 12 | Schedule Triggers | 114-118 | 1h | Loose |
| 13 | Document Templates | 119-126 | 1-2h | Loose |
| 14 | Workflow Templates | 127-132 | 1h | Loose |
| 15 | Observability | 133-142 | 1-2h | Loose |
| 16 | Background Runner | 143-150 | 1-2h | Loose |
| 17 | Production Hardening | 151-160 | 1-2h | **STRICT** ⚠️ |

**Total: 8-12 hours** (vs 12-26 hours for 160-step approach)

---

## Bundle Details

---

### Bundle 1: Minimal E2E Workflow ✅

**Goal**: Execute a simple 2-node linear workflow via API, store results in PostgreSQL, query execution status.

**Reference**: v1 Steps 1-30 (detailed breakdown), Spec §2 Architecture, §3.1 Database, §4.1 Workflow Schema, §7 Actions, §9.2 Execution, §13.1 API

**Auth Mode**: `AllowLooseAuth=true`

**What You're Building:**
```
User → POST /workflows/test/execute
     → Conductor executes Node1 (core.echo) → Node2 (core.echo)
     → Results stored in DB
     → Returns executionId
User → GET /executions/{id}
     → Returns workflow status + results
```

---

#### **Inputs**

**Spec Sections:**
- §2.1 Components | Lines 42-88 (approx) - Architecture overview
- §3.1 Core Workflow Tables | Lines 131-207 (approx) - Database schema
- §4.1 Workflow Definition Schema | Lines 642-757 (approx) - JSON format
- §7.1-7.4 Action Contract | Lines 849-901 (approx) - IWorkflowAction interface
- §9.2 Run Loop | Lines 1076-1087 (approx) - Execution logic
- §13.1 Workflow Execution | Lines 1349-1378 (approx) - API endpoints

**Existing Files:**
- `src/DataWorkflows.Engine/Program.cs` (has health checks from your work)
- `src/DataWorkflows.Engine/appsettings.json`
- `DataWorkflows.sln`

**External Dependencies:**
- PostgreSQL running (Docker Compose)
- Connection string configured

---

#### **Implementation**

**Database Schema** (create these migrations):

1. **`src/DataWorkflows.Data/Migrations/001_CreateWorkflows.sql`**
```sql
CREATE TABLE IF NOT EXISTS Workflows (
  Id              TEXT PRIMARY KEY,
  DisplayName     TEXT NOT NULL,
  CurrentVersion  INT NULL,
  Status          TEXT NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Active','Archived')),
  IsEnabled       BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

2. **`src/DataWorkflows.Data/Migrations/002_CreateWorkflowDefinitions.sql`**
```sql
CREATE TABLE IF NOT EXISTS WorkflowDefinitions (
  WorkflowId      TEXT        NOT NULL,
  Version         INT         NOT NULL,
  DefinitionJson  JSONB       NOT NULL,
  Checksum        TEXT        NOT NULL,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (WorkflowId, Version),
  CONSTRAINT uq_workflow_checksum UNIQUE (WorkflowId, Checksum),
  FOREIGN KEY (WorkflowId) REFERENCES Workflows(Id) ON DELETE CASCADE
);
```

3. **`src/DataWorkflows.Data/Migrations/003_CreateWorkflowExecutions.sql`**
```sql
CREATE TABLE IF NOT EXISTS WorkflowExecutions (
  Id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowId        TEXT NOT NULL,
  WorkflowVersion   INT  NOT NULL,
  WorkflowRequestId TEXT NOT NULL,
  Status            TEXT NOT NULL CHECK (Status IN ('Pending','Running','Succeeded','Failed','Cancelled')),
  TriggerPayloadJson JSONB NOT NULL,
  StartTime         TIMESTAMPTZ NULL,
  EndTime           TIMESTAMPTZ NULL,
  CorrelationId     TEXT NULL,
  FOREIGN KEY (WorkflowId, WorkflowVersion)
    REFERENCES WorkflowDefinitions(WorkflowId, Version) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX ux_wfexec_workflow_request
  ON WorkflowExecutions(WorkflowId, WorkflowRequestId);
```

4. **`src/DataWorkflows.Data/Migrations/004_CreateActionExecutions.sql`**
```sql
CREATE TABLE IF NOT EXISTS ActionExecutions (
  Id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowExecutionId UUID NOT NULL,
  NodeId              TEXT NOT NULL,
  ActionType          TEXT NOT NULL,
  Status              TEXT NOT NULL CHECK (Status IN ('Succeeded','Failed','RetriableFailure','Skipped')),
  Attempt             INT NOT NULL DEFAULT 1,
  OutputsJson         JSONB NULL,
  ErrorJson           JSONB NULL,
  StartTime           TIMESTAMPTZ NULL,
  EndTime             TIMESTAMPTZ NULL,
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX ix_actionexec_by_exec_node
  ON ActionExecutions(WorkflowExecutionId, NodeId);
```

---

**Repositories** (Dapper):

5. **`src/DataWorkflows.Data/Repositories/WorkflowExecutionRepository.cs`**
```csharp
using Dapper;
using Npgsql;

namespace DataWorkflows.Data.Repositories;

public class WorkflowExecutionRepository {
  private readonly string _connectionString;

  public WorkflowExecutionRepository(string connectionString) {
    _connectionString = connectionString;
  }

  public async Task<Guid> CreateExecution(string workflowId, int version, string requestId, string triggerJson) {
    using var conn = new NpgsqlConnection(_connectionString);
    var sql = @"
      INSERT INTO WorkflowExecutions (WorkflowId, WorkflowVersion, WorkflowRequestId, Status, TriggerPayloadJson, CorrelationId)
      VALUES (@WorkflowId, @Version, @RequestId, 'Pending', @TriggerJson::jsonb, @CorrelationId)
      RETURNING Id";

    return await conn.ExecuteScalarAsync<Guid>(sql, new {
      WorkflowId = workflowId,
      Version = version,
      RequestId = requestId,
      TriggerJson = triggerJson,
      CorrelationId = Guid.NewGuid().ToString()
    });
  }

  public async Task<WorkflowExecution?> GetById(Guid id) {
    using var conn = new NpgsqlConnection(_connectionString);
    var sql = "SELECT * FROM WorkflowExecutions WHERE Id = @Id";
    return await conn.QuerySingleOrDefaultAsync<WorkflowExecution>(sql, new { Id = id });
  }
}

public record WorkflowExecution(
  Guid Id,
  string WorkflowId,
  int WorkflowVersion,
  string WorkflowRequestId,
  string Status,
  string TriggerPayloadJson,
  DateTime? StartTime,
  DateTime? EndTime
);
```

6. **`src/DataWorkflows.Data/Repositories/ActionExecutionRepository.cs`**
```csharp
using Dapper;
using Npgsql;

namespace DataWorkflows.Data.Repositories;

public class ActionExecutionRepository {
  private readonly string _connectionString;

  public ActionExecutionRepository(string connectionString) {
    _connectionString = connectionString;
  }

  public async Task RecordExecution(
    Guid executionId,
    string nodeId,
    string actionType,
    string status,
    string? outputs,
    DateTime startTime,
    DateTime endTime
  ) {
    using var conn = new NpgsqlConnection(_connectionString);
    var sql = @"
      INSERT INTO ActionExecutions (WorkflowExecutionId, NodeId, ActionType, Status, OutputsJson, StartTime, EndTime)
      VALUES (@ExecutionId, @NodeId, @ActionType, @Status, @Outputs::jsonb, @StartTime, @EndTime)";

    await conn.ExecuteAsync(sql, new {
      ExecutionId = executionId,
      NodeId = nodeId,
      ActionType = actionType,
      Status = status,
      Outputs = outputs,
      StartTime = startTime,
      EndTime = endTime
    });
  }
}
```

---

**Models**:

7. **`src/DataWorkflows.Engine/Models/WorkflowDefinition.cs`**
```csharp
namespace DataWorkflows.Engine.Models;

public sealed record WorkflowDefinition(
  string Id,
  string DisplayName,
  string StartNode,
  List<Node> Nodes
);
```

8. **`src/DataWorkflows.Engine/Models/Node.cs`**
```csharp
namespace DataWorkflows.Engine.Models;

public sealed record Node(
  string Id,
  string ActionType,
  Dictionary<string, object>? Parameters = null,
  List<Edge>? Edges = null
);
```

9. **`src/DataWorkflows.Engine/Models/Edge.cs`**
```csharp
namespace DataWorkflows.Engine.Models;

public sealed record Edge(
  string TargetNode,
  string When = "success",
  string? Condition = null
);
```

10. **`src/DataWorkflows.Engine/Models/ExecutionResult.cs`**
```csharp
namespace DataWorkflows.Engine.Models;

public record ExecutionResult(
  Guid ExecutionId,
  string Status,
  DateTime CompletedAt
);
```

11. **`src/DataWorkflows.Engine/Models/ExecuteRequest.cs`**
```csharp
namespace DataWorkflows.Engine.Models;

public record ExecuteRequest(Dictionary<string, object>? Trigger);
```

---

**Parsing**:

12. **`src/DataWorkflows.Engine/Parsing/WorkflowParser.cs`**
```csharp
using System.Text.Json;
using DataWorkflows.Engine.Models;

namespace DataWorkflows.Engine.Parsing;

public class WorkflowParser {
  private static readonly JsonSerializerOptions _options = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  public WorkflowDefinition Parse(string json) {
    return JsonSerializer.Deserialize<WorkflowDefinition>(json, _options)
      ?? throw new ArgumentException("Invalid workflow JSON");
  }
}
```

---

**Action System**:

13. **`src/DataWorkflows.Contracts/Actions/IWorkflowAction.cs`**
```csharp
namespace DataWorkflows.Contracts.Actions;

public interface IWorkflowAction {
  string Type { get; }
  Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}
```

14. **`src/DataWorkflows.Contracts/Actions/ActionExecutionContext.cs`**
```csharp
namespace DataWorkflows.Contracts.Actions;

public sealed record ActionExecutionContext(
  Guid WorkflowExecutionId,
  string NodeId,
  Dictionary<string, object?> Parameters,
  IServiceProvider Services
);
```

15. **`src/DataWorkflows.Contracts/Actions/ActionExecutionResult.cs`**
```csharp
namespace DataWorkflows.Contracts.Actions;

public sealed record ActionExecutionResult(
  ActionExecutionStatus Status,
  Dictionary<string, object?> Outputs,
  string? ErrorMessage = null
);
```

16. **`src/DataWorkflows.Contracts/Actions/ActionExecutionStatus.cs`**
```csharp
namespace DataWorkflows.Contracts.Actions;

public enum ActionExecutionStatus {
  Succeeded,
  Failed,
  RetriableFailure,
  Skipped
}
```

17. **`src/DataWorkflows.Engine/Actions/CoreEchoAction.cs`**
```csharp
using DataWorkflows.Contracts.Actions;

namespace DataWorkflows.Engine.Actions;

public class CoreEchoAction : IWorkflowAction {
  public string Type => "core.echo";

  public Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct) {
    var message = context.Parameters.TryGetValue("message", out var msg) ? msg?.ToString() : "echo";

    return Task.FromResult(new ActionExecutionResult(
      Status: ActionExecutionStatus.Succeeded,
      Outputs: new Dictionary<string, object?> { ["echo"] = message }
    ));
  }
}
```

18. **`src/DataWorkflows.Engine/Registry/ActionRegistry.cs`**
```csharp
using DataWorkflows.Contracts.Actions;
using DataWorkflows.Engine.Actions;

namespace DataWorkflows.Engine.Registry;

public class ActionRegistry {
  private readonly Dictionary<string, IWorkflowAction> _actions = new();

  public ActionRegistry() {
    Register(new CoreEchoAction());
  }

  public void Register(IWorkflowAction action) {
    _actions[action.Type] = action;
  }

  public IWorkflowAction GetAction(string actionType) {
    return _actions.TryGetValue(actionType, out var action)
      ? action
      : throw new KeyNotFoundException($"Action not found: {actionType}");
  }
}
```

---

**Execution**:

19. **`src/DataWorkflows.Engine/Execution/WorkflowContext.cs`**
```csharp
using System.Collections.Concurrent;

namespace DataWorkflows.Engine.Execution;

public sealed class WorkflowContext {
  private readonly ConcurrentDictionary<string, object?> _data = new();

  public void SetActionOutput(string nodeId, object? output) => _data[nodeId] = output;

  public Dictionary<string, object?> GetAllOutputs() =>
    _data.ToDictionary(kv => kv.Key, kv => kv.Value);
}
```

20. **`src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs`**
```csharp
using DataWorkflows.Contracts.Actions;
using DataWorkflows.Data.Repositories;
using DataWorkflows.Engine.Execution;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Registry;

namespace DataWorkflows.Engine.Orchestration;

public class WorkflowConductor {
  private readonly ActionRegistry _registry;

  public WorkflowConductor(ActionRegistry registry) {
    _registry = registry;
  }

  public async Task<ExecutionResult> ExecuteAsync(
    Guid executionId,
    WorkflowDefinition workflow,
    Dictionary<string, object> trigger,
    string connectionString
  ) {
    var context = new WorkflowContext();
    var actionRepo = new ActionExecutionRepository(connectionString);

    // Simple linear execution (ignore edges for now)
    foreach (var node in workflow.Nodes) {
      var startTime = DateTime.UtcNow;
      var action = _registry.GetAction(node.ActionType);

      var actionContext = new ActionExecutionContext(
        WorkflowExecutionId: executionId,
        NodeId: node.Id,
        Parameters: node.Parameters ?? new(),
        Services: null!
      );

      var result = await action.ExecuteAsync(actionContext, CancellationToken.None);

      await actionRepo.RecordExecution(
        executionId: executionId,
        nodeId: node.Id,
        actionType: node.ActionType,
        status: result.Status.ToString(),
        outputs: System.Text.Json.JsonSerializer.Serialize(result.Outputs),
        startTime: startTime,
        endTime: DateTime.UtcNow
      );

      if (result.Status == ActionExecutionStatus.Succeeded) {
        context.SetActionOutput(node.Id, result.Outputs);
      } else {
        throw new Exception($"Action failed: {result.ErrorMessage}");
      }
    }

    return new ExecutionResult(
      ExecutionId: executionId,
      Status: "Succeeded",
      CompletedAt: DateTime.UtcNow
    );
  }
}
```

---

**API Controllers**:

21. **`src/DataWorkflows.Engine/Controllers/ExecuteController.cs`**
```csharp
using Microsoft.AspNetCore.Mvc;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Parsing;
using DataWorkflows.Engine.Orchestration;
using DataWorkflows.Engine.Registry;

namespace DataWorkflows.Engine.Controllers;

[ApiController]
[Route("api/v1/workflows")]
public class ExecuteController : ControllerBase {
  private readonly IConfiguration _config;

  public ExecuteController(IConfiguration config) {
    _config = config;
  }

  [HttpPost("{workflowId}/execute")]
  public async Task<IActionResult> Execute(string workflowId, [FromBody] ExecuteRequest request) {
    // Hardcoded workflow for Bundle 1 (Bundle 7 will use DB)
    var workflowJson = """
    {
      "id": "test",
      "displayName": "Test Workflow",
      "startNode": "echo1",
      "nodes": [
        { "id": "echo1", "actionType": "core.echo", "parameters": { "message": "Hello" } },
        { "id": "echo2", "actionType": "core.echo", "parameters": { "message": "World" } }
      ]
    }
    """;

    var parser = new WorkflowParser();
    var workflow = parser.Parse(workflowJson);

    var conductor = new WorkflowConductor(new ActionRegistry());
    var executionId = Guid.NewGuid();

    var connectionString = _config.GetConnectionString("Postgres")!;
    var result = await conductor.ExecuteAsync(executionId, workflow, request.Trigger ?? new(), connectionString);

    return Accepted(new {
      executionId = result.ExecutionId,
      status = result.Status,
      statusUrl = $"/api/v1/executions/{result.ExecutionId}"
    });
  }
}
```

22. **`src/DataWorkflows.Engine/Controllers/ExecutionsController.cs`**
```csharp
using Microsoft.AspNetCore.Mvc;
using DataWorkflows.Data.Repositories;

namespace DataWorkflows.Engine.Controllers;

[ApiController]
[Route("api/v1/executions")]
public class ExecutionsController : ControllerBase {
  private readonly IConfiguration _config;

  public ExecutionsController(IConfiguration config) {
    _config = config;
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetExecution(Guid id) {
    var connectionString = _config.GetConnectionString("Postgres")!;
    var repo = new WorkflowExecutionRepository(connectionString);
    var execution = await repo.GetById(id);

    return execution != null ? Ok(execution) : NotFound();
  }
}
```

---

**Test Fixtures**:

23. **`fixtures/bundle1/simple-echo-workflow.json`**
```json
{
  "id": "simple-echo",
  "displayName": "Simple Echo Workflow",
  "startNode": "echo1",
  "nodes": [
    {
      "id": "echo1",
      "actionType": "core.echo",
      "parameters": {
        "message": "Hello from node 1"
      }
    },
    {
      "id": "echo2",
      "actionType": "core.echo",
      "parameters": {
        "message": "Hello from node 2"
      }
    }
  ]
}
```

24. **`fixtures/bundle1/execute-request.json`**
```json
{
  "trigger": {
    "source": "manual",
    "userId": "test-user-123",
    "timestamp": "2025-01-15T10:00:00Z"
  }
}
```

---

#### **Checkpoint**

**Run migrations:**
```bash
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/001_CreateWorkflows.sql
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/002_CreateWorkflowDefinitions.sql
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/003_CreateWorkflowExecutions.sql
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/004_CreateActionExecutions.sql
```

**Verify build:**
```bash
dotnet build
# Should succeed with 0 errors
```

**Start API:**
```bash
dotnet run --project src/DataWorkflows.Engine
# Should start on port 5131
```

**Test E2E workflow:**
```bash
# Execute workflow
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d @fixtures/bundle1/execute-request.json \
  | jq '.' | tee /tmp/execution-response.json

# Save executionId
EXEC_ID=$(jq -r '.executionId' /tmp/execution-response.json)
echo "Execution ID: $EXEC_ID"

# Query execution status
curl http://localhost:5131/api/v1/executions/$EXEC_ID | jq '.'

# Expected response:
{
  "id": "...",
  "workflowId": "test",
  "workflowVersion": 1,
  "status": "Succeeded",
  "triggerPayloadJson": "{\"source\":\"manual\",\"userId\":\"test-user-123\",...}",
  "startTime": "...",
  "endTime": "..."
}
```

**Verify database:**
```bash
# Check ActionExecutions
psql -h localhost -U postgres -d dataworkflows -c "
  SELECT NodeId, ActionType, Status, OutputsJson
  FROM ActionExecutions
  WHERE WorkflowExecutionId = '$EXEC_ID'
  ORDER BY StartTime;
"

# Expected: 2 rows (echo1, echo2), both Succeeded
```

**Success Criteria:**
✅ API returns 202 Accepted with executionId
✅ GET /executions/{id} returns execution with Status=Succeeded
✅ Database has 2 ActionExecutions rows with correct outputs
✅ Health check still works: `curl http://localhost:5131/health/live`

**Commit:**
```bash
git add .
git commit -m "Bundle 1: Minimal E2E workflow engine ✅

- Database schema (4 tables)
- Workflow parser + models
- Action system (IWorkflowAction + core.echo)
- Linear conductor (no retries, no branching)
- Execute + query API endpoints
- E2E test passes"
```

---

### Bundle 2: Retry & Error Handling ⚡

**Goal**: Add retry policies, transient vs permanent failure handling, and fail-fast cancellation.

**Reference**: v1 Steps 31-40, Spec §10.1 Orchestration Options | Lines 1127-1141 (approx)

**Auth Mode**: `AllowLooseAuth=true`

**What You're Adding:**
```
Bundle 1: Workflows execute, but fail permanently on any error
Bundle 2: Workflows retry transient failures (RetriableFailure status)
         + Fail-fast on permanent failures
         + Record each attempt in DB
```

---

#### **Inputs**

**From Bundle 1:**
- All Bundle 1 files (working E2E system)
- WorkflowConductor.cs (will modify)
- ActionExecutions table (will add Attempt/RetryCount columns)

**Spec Sections:**
- §10.1 Orchestration Options | Lines 1127-1141 (approx) - RetryPolicyOptions
- §9.3 Retry Semantics | Lines 1089-1098 (approx) - Retry behavior

**External Dependencies:**
- Polly (already in project from Step 2)

---

#### **Implementation**

**Database Migration:**

1. **`src/DataWorkflows.Data/Migrations/005_AddRetryColumns.sql`**
```sql
ALTER TABLE ActionExecutions
  ADD COLUMN RetryCount INT NOT NULL DEFAULT 0;

-- Attempt already exists from Bundle 1
-- Just verify it's there with default 1
```

**Add Polly Retry Policy:**

2. **Modify `src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs`**

Add using:
```csharp
using Polly;
using Polly.Retry;
```

Update ExecuteAsync method to wrap action execution:
```csharp
public async Task<ExecutionResult> ExecuteAsync(
  Guid executionId,
  WorkflowDefinition workflow,
  Dictionary<string, object> trigger,
  string connectionString
) {
  var context = new WorkflowContext();
  var actionRepo = new ActionExecutionRepository(connectionString);
  var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // Workflow timeout

  // Define retry policy
  var retryPolicy = Policy
    .Handle<Exception>(ex => IsRetriable(ex))
    .WaitAndRetryAsync(
      retryCount: 3,
      sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
      onRetry: (exception, timeSpan, retryCount, context) => {
        Console.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds}s due to: {exception.Message}");
      }
    );

  foreach (var node in workflow.Nodes) {
    if (cts.Token.IsCancellationRequested) {
      // Mark remaining nodes as Skipped
      await actionRepo.RecordExecution(
        executionId, node.Id, node.ActionType, "Skipped", null, DateTime.UtcNow, DateTime.UtcNow
      );
      continue;
    }

    var action = _registry.GetAction(node.ActionType);
    var actionContext = new ActionExecutionContext(
      WorkflowExecutionId: executionId,
      NodeId: node.Id,
      Parameters: node.Parameters ?? new(),
      Services: null!
    );

    ActionExecutionResult? result = null;
    int attempt = 1;

    try {
      result = await retryPolicy.ExecuteAsync(async () => {
        var startTime = DateTime.UtcNow;
        var execResult = await action.ExecuteAsync(actionContext, cts.Token);

        await actionRepo.RecordExecution(
          executionId: executionId,
          nodeId: node.Id,
          actionType: node.ActionType,
          status: execResult.Status.ToString(),
          outputs: System.Text.Json.JsonSerializer.Serialize(execResult.Outputs),
          startTime: startTime,
          endTime: DateTime.UtcNow
        );

        if (execResult.Status == ActionExecutionStatus.RetriableFailure) {
          throw new RetriableException(execResult.ErrorMessage ?? "Transient failure");
        }

        return execResult;
      });
    } catch (Exception ex) when (ex is not RetriableException) {
      // Permanent failure - cancel workflow
      await actionRepo.RecordExecution(
        executionId, node.Id, node.ActionType, "Failed",
        System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message }),
        DateTime.UtcNow, DateTime.UtcNow
      );
      cts.Cancel(); // Fail-fast
      throw;
    }

    if (result != null && result.Status == ActionExecutionStatus.Succeeded) {
      context.SetActionOutput(node.Id, result.Outputs);
    }
  }

  return new ExecutionResult(
    ExecutionId: executionId,
    Status: cts.Token.IsCancellationRequested ? "Failed" : "Succeeded",
    CompletedAt: DateTime.UtcNow
  );
}

private bool IsRetriable(Exception ex) {
  return ex is RetriableException || ex is TimeoutException || ex is HttpRequestException;
}

public class RetriableException : Exception {
  public RetriableException(string message) : base(message) { }
}
```

**Update CoreEchoAction to Support Simulated Failures:**

3. **Modify `src/DataWorkflows.Engine/Actions/CoreEchoAction.cs`**
```csharp
using DataWorkflows.Contracts.Actions;

namespace DataWorkflows.Engine.Actions;

public class CoreEchoAction : IWorkflowAction {
  public string Type => "core.echo";
  private int _attemptCount = 0;

  public Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct) {
    var message = context.Parameters.TryGetValue("message", out var msg) ? msg?.ToString() : "echo";

    // Support simulated failures for testing
    if (context.Parameters.TryGetValue("simulateFailure", out var simFailure) && simFailure is string failType) {
      _attemptCount++;

      if (failType == "transient" && _attemptCount < 3) {
        return Task.FromResult(new ActionExecutionResult(
          Status: ActionExecutionStatus.RetriableFailure,
          Outputs: new Dictionary<string, object?>(),
          ErrorMessage: $"Simulated transient failure (attempt {_attemptCount})"
        ));
      }

      if (failType == "permanent") {
        return Task.FromResult(new ActionExecutionResult(
          Status: ActionExecutionStatus.Failed,
          Outputs: new Dictionary<string, object?>(),
          ErrorMessage: "Simulated permanent failure"
        ));
      }
    }

    return Task.FromResult(new ActionExecutionResult(
      Status: ActionExecutionStatus.Succeeded,
      Outputs: new Dictionary<string, object?> { ["echo"] = message }
    ));
  }
}
```

**Test Fixtures:**

4. **`fixtures/bundle2/retry-workflow.json`**
```json
{
  "id": "retry-test",
  "displayName": "Retry Test Workflow",
  "startNode": "transient-fail",
  "nodes": [
    {
      "id": "transient-fail",
      "actionType": "core.echo",
      "parameters": {
        "message": "Will succeed after retries",
        "simulateFailure": "transient"
      }
    },
    {
      "id": "success",
      "actionType": "core.echo",
      "parameters": {
        "message": "Success after retry"
      }
    }
  ]
}
```

---

#### **Checkpoint**

**Run migration:**
```bash
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/005_AddRetryColumns.sql
```

**Build:**
```bash
dotnet build
# Should succeed
```

**Test retry logic:**
```bash
# Start API
dotnet run --project src/DataWorkflows.Engine

# Test transient failure (should retry and succeed)
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d '{
    "trigger": {
      "source": "retry-test"
    }
  }'

# Check logs - should show retry attempts
# Check DB - should show multiple ActionExecutions rows for same node
```

**Success Criteria:**
✅ Workflows retry on RetriableFailure
✅ Workflows fail-fast on permanent failures
✅ Each attempt recorded in ActionExecutions
✅ Bundle 1 workflows still work

**Commit:**
```bash
git commit -am "Bundle 2: Add retry logic and error handling ✅

- Polly retry policy (3 attempts, exponential backoff)
- RetriableFailure vs Failed status handling
- Fail-fast cancellation
- Attempt tracking in DB"
```

---

### Bundle 3: Branching & Conditions 🌳

**Goal**: Add Jint condition evaluation, edge routing, superset graph validation, join nodes, and parallel execution.

**Reference**: v1 Steps 41-50, Spec §6 Condition Evaluator | Lines 817-843 (approx), §9.1.2 Runtime Planning | Lines 1063-1074 (approx)

**Auth Mode**: `AllowLooseAuth=true`

**What You're Adding:**
```
Bundle 2: Workflows execute nodes sequentially
Bundle 3: Workflows can branch based on conditions
         + Multiple edges per node
         + Parallel execution
         + Join nodes (wait for all incoming edges)
```

---

#### **Inputs**

**From Bundle 2:**
- WorkflowConductor.cs (will rewrite execution loop)
- Node.cs (already has Edges property)
- Edge.cs (already has Condition property)

**Spec Sections:**
- §6 Condition Evaluator | Lines 817-843 (approx) - Jint configuration
- §4.3 Routing Semantics | Lines 771-777 (approx) - Edge satisfaction
- §9.2 Run Loop Bounded Concurrency | Lines 1076-1087 (approx) - Parallel execution

**External Dependencies:**
- Jint (already added in Step 2)

---

#### **Implementation**

**Condition Evaluator:**

1. **`src/DataWorkflows.Engine/Evaluation/JintConditionEvaluator.cs`**
```csharp
using Jint;
using Jint.Runtime;

namespace DataWorkflows.Engine.Evaluation;

public class JintConditionEvaluator {
  private readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

  public bool Evaluate(string condition, Dictionary<string, object?> contextData) {
    if (string.IsNullOrWhiteSpace(condition)) {
      return true; // No condition = always satisfied
    }

    try {
      var engine = new Engine(options => {
        options.TimeoutInterval(_timeout);
        options.LimitRecursion(10);
      });

      // Provide read-only context
      foreach (var kvp in contextData) {
        engine.SetValue(kvp.Key, kvp.Value);
      }

      var result = engine.Evaluate(condition);
      return result.AsBoolean();
    } catch (JavaScriptException ex) {
      Console.WriteLine($"Condition evaluation error: {ex.Message}");
      return false; // Treat errors as false
    } catch (Exception ex) {
      Console.WriteLine($"Condition evaluation timeout or error: {ex.Message}");
      return false;
    }
  }
}
```

**Graph Validator:**

2. **`src/DataWorkflows.Engine/Validation/GraphValidator.cs`**
```csharp
using DataWorkflows.Engine.Models;

namespace DataWorkflows.Engine.Validation;

public class GraphValidator {
  public void Validate(WorkflowDefinition workflow) {
    // Check startNode exists
    if (!workflow.Nodes.Any(n => n.Id == workflow.StartNode)) {
      throw new ArgumentException($"startNode '{workflow.StartNode}' not found in nodes");
    }

    // Check all edge targets exist
    foreach (var node in workflow.Nodes) {
      if (node.Edges != null) {
        foreach (var edge in node.Edges) {
          if (!workflow.Nodes.Any(n => n.Id == edge.TargetNode)) {
            throw new ArgumentException($"Edge target '{edge.TargetNode}' not found (from node '{node.Id}')");
          }
        }
      }
    }

    // Check for cycles (simple DFS check)
    var visited = new HashSet<string>();
    var recStack = new HashSet<string>();

    bool HasCycle(string nodeId) {
      if (recStack.Contains(nodeId)) return true;
      if (visited.Contains(nodeId)) return false;

      visited.Add(nodeId);
      recStack.Add(nodeId);

      var node = workflow.Nodes.First(n => n.Id == nodeId);
      if (node.Edges != null) {
        foreach (var edge in node.Edges) {
          if (HasCycle(edge.TargetNode)) return true;
        }
      }

      recStack.Remove(nodeId);
      return false;
    }

    if (HasCycle(workflow.StartNode)) {
      throw new ArgumentException("Workflow contains a cycle (not a DAG)");
    }
  }
}
```

**Rewrite Conductor for Parallel Execution:**

3. **Replace `src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs`**

(This is a significant rewrite - see v1 Steps 45-47 for detailed breakdown)

```csharp
using System.Collections.Concurrent;
using DataWorkflows.Contracts.Actions;
using DataWorkflows.Data.Repositories;
using DataWorkflows.Engine.Evaluation;
using DataWorkflows.Engine.Execution;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Registry;
using DataWorkflows.Engine.Validation;
using Polly;

namespace DataWorkflows.Engine.Orchestration;

public class WorkflowConductor {
  private readonly ActionRegistry _registry;
  private readonly SemaphoreSlim _semaphore = new(10); // Max 10 parallel actions

  public WorkflowConductor(ActionRegistry registry) {
    _registry = registry;
  }

  public async Task<ExecutionResult> ExecuteAsync(
    Guid executionId,
    WorkflowDefinition workflow,
    Dictionary<string, object> trigger,
    string connectionString
  ) {
    // Validate graph
    var validator = new GraphValidator();
    validator.Validate(workflow);

    var context = new WorkflowContext();
    var actionRepo = new ActionExecutionRepository(connectionString);
    var evaluator = new JintConditionEvaluator();
    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    // Track node completion
    var completed = new ConcurrentDictionary<string, ActionExecutionStatus>();
    var incomingEdges = BuildIncomingEdgeMap(workflow);
    var runQueue = new BlockingCollection<string>();

    runQueue.Add(workflow.StartNode);

    var tasks = new List<Task>();

    while (!runQueue.IsCompleted || tasks.Any()) {
      // Dequeue and execute nodes
      while (runQueue.TryTake(out var nodeId, 100)) {
        if (cts.Token.IsCancellationRequested) break;

        var node = workflow.Nodes.First(n => n.Id == nodeId);

        var task = Task.Run(async () => {
          await _semaphore.WaitAsync(cts.Token);
          try {
            var status = await ExecuteNodeAsync(executionId, node, context, actionRepo, cts.Token);
            completed[nodeId] = status;

            if (status == ActionExecutionStatus.Succeeded) {
              // Evaluate outgoing edges
              if (node.Edges != null) {
                foreach (var edge in node.Edges) {
                  var edgeSatisfied = EvaluateEdge(edge, status, context.GetAllOutputs(), evaluator);

                  if (edgeSatisfied) {
                    // Check if target is ready (all incoming edges satisfied)
                    if (IsNodeReady(edge.TargetNode, incomingEdges, completed)) {
                      runQueue.Add(edge.TargetNode);
                    }
                  }
                }
              }
            } else {
              // Failure - cancel workflow
              cts.Cancel();
            }
          } finally {
            _semaphore.Release();
          }
        }, cts.Token);

        tasks.Add(task);
      }

      // Clean up completed tasks
      if (tasks.Any()) {
        var completed = await Task.WhenAny(tasks);
        tasks.Remove(completed);
      }

      // Check if all nodes processed
      if (runQueue.Count == 0 && !tasks.Any()) {
        runQueue.CompleteAdding();
      }
    }

    await Task.WhenAll(tasks);

    return new ExecutionResult(
      ExecutionId: executionId,
      Status: cts.Token.IsCancellationRequested ? "Failed" : "Succeeded",
      CompletedAt: DateTime.UtcNow
    );
  }

  private async Task<ActionExecutionStatus> ExecuteNodeAsync(
    Guid executionId,
    Node node,
    WorkflowContext context,
    ActionExecutionRepository repo,
    CancellationToken ct
  ) {
    var action = _registry.GetAction(node.ActionType);
    var actionContext = new ActionExecutionContext(
      WorkflowExecutionId: executionId,
      NodeId: node.Id,
      Parameters: node.Parameters ?? new(),
      Services: null!
    );

    var startTime = DateTime.UtcNow;
    var result = await action.ExecuteAsync(actionContext, ct);

    await repo.RecordExecution(
      executionId: executionId,
      nodeId: node.Id,
      actionType: node.ActionType,
      status: result.Status.ToString(),
      outputs: System.Text.Json.JsonSerializer.Serialize(result.Outputs),
      startTime: startTime,
      endTime: DateTime.UtcNow
    );

    if (result.Status == ActionExecutionStatus.Succeeded) {
      context.SetActionOutput(node.Id, result.Outputs);
    }

    return result.Status;
  }

  private bool EvaluateEdge(Edge edge, ActionExecutionStatus nodeStatus, Dictionary<string, object?> contextData, JintConditionEvaluator evaluator) {
    // Check 'when' clause
    var whenSatisfied = edge.When switch {
      "always" => true,
      "success" => nodeStatus == ActionExecutionStatus.Succeeded,
      "failure" => nodeStatus == ActionExecutionStatus.Failed,
      _ => false
    };

    if (!whenSatisfied) return false;

    // Check condition
    if (!string.IsNullOrEmpty(edge.Condition)) {
      return evaluator.Evaluate(edge.Condition, contextData);
    }

    return true;
  }

  private Dictionary<string, List<string>> BuildIncomingEdgeMap(WorkflowDefinition workflow) {
    var map = new Dictionary<string, List<string>>();

    foreach (var node in workflow.Nodes) {
      if (node.Edges != null) {
        foreach (var edge in node.Edges) {
          if (!map.ContainsKey(edge.TargetNode)) {
            map[edge.TargetNode] = new List<string>();
          }
          map[edge.TargetNode].Add(node.Id);
        }
      }
    }

    return map;
  }

  private bool IsNodeReady(string nodeId, Dictionary<string, List<string>> incomingEdges, ConcurrentDictionary<string, ActionExecutionStatus> completed) {
    if (!incomingEdges.ContainsKey(nodeId)) {
      return true; // No incoming edges = always ready
    }

    var incoming = incomingEdges[nodeId];
    return incoming.All(parentId => completed.ContainsKey(parentId) && completed[parentId] == ActionExecutionStatus.Succeeded);
  }
}
```

**Test Fixtures:**

4. **`fixtures/bundle3/fanout-fanin-workflow.json`**
```json
{
  "id": "fanout-fanin",
  "displayName": "Fan-out/Fan-in Test",
  "startNode": "start",
  "nodes": [
    {
      "id": "start",
      "actionType": "core.echo",
      "parameters": { "message": "Start" },
      "edges": [
        { "targetNode": "branch-a", "when": "success" },
        { "targetNode": "branch-b", "when": "success" }
      ]
    },
    {
      "id": "branch-a",
      "actionType": "core.echo",
      "parameters": { "message": "Branch A" },
      "edges": [
        { "targetNode": "join", "when": "success" }
      ]
    },
    {
      "id": "branch-b",
      "actionType": "core.echo",
      "parameters": { "message": "Branch B" },
      "edges": [
        { "targetNode": "join", "when": "success" }
      ]
    },
    {
      "id": "join",
      "actionType": "core.echo",
      "parameters": { "message": "Joined!" }
    }
  ]
}
```

5. **`fixtures/bundle3/conditional-branch-workflow.json`**
```json
{
  "id": "conditional",
  "displayName": "Conditional Branching Test",
  "startNode": "check",
  "nodes": [
    {
      "id": "check",
      "actionType": "core.echo",
      "parameters": { "message": "Checking..." },
      "edges": [
        {
          "targetNode": "approved",
          "when": "success",
          "condition": "trigger.status === 'approved'"
        },
        {
          "targetNode": "rejected",
          "when": "success",
          "condition": "trigger.status !== 'approved'"
        }
      ]
    },
    {
      "id": "approved",
      "actionType": "core.echo",
      "parameters": { "message": "Approved!" }
    },
    {
      "id": "rejected",
      "actionType": "core.echo",
      "parameters": { "message": "Rejected!" }
    }
  ]
}
```

---

#### **Checkpoint**

**Build:**
```bash
dotnet build
```

**Test fan-out/fan-in:**
```bash
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d '{
    "trigger": {
      "source": "fanout-test"
    }
  }'

# Check DB - should show 4 ActionExecutions (start, branch-a, branch-b, join)
# Verify branch-a and branch-b ran in parallel (similar timestamps)
# Verify join ran after both branches completed
```

**Test conditional branching:**
```bash
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d '{
    "trigger": {
      "source": "conditional-test",
      "status": "approved"
    }
  }'

# Should execute: check → approved (NOT rejected)
```

**Success Criteria:**
✅ Workflows can branch based on conditions
✅ Multiple edges per node work
✅ Join nodes wait for all incoming edges
✅ Parallel execution works (semaphore limits to 10)
✅ Bundle 1-2 workflows still work

**Commit:**
```bash
git commit -am "Bundle 3: Add branching, conditions, and parallel execution ✅

- Jint condition evaluator
- Graph validator (cycle detection)
- Superset graph + incoming edge map
- Join node logic
- Bounded parallel execution (max 10)"
```

---

### Bundle 4-17: Remaining Features

**[Bundle 4-17 follow the same structure as above]**

Each bundle:
1. **Goal** - What feature is complete after this bundle
2. **Reference** - v1 steps + spec sections
3. **Inputs** - What you need to read
4. **Implementation** - All code changes
5. **Checkpoint** - Tests to verify it works

**Remaining bundles** (summarized):

- **Bundle 4**: Parameter templating (Scriban)
- **Bundle 5**: Monday connector (real API integration)
- **Bundle 6**: Slack connector
- **Bundle 7**: Workflow lifecycle (Draft/Publish/Archive)
- **Bundle 8**: Principal tracking
- **Bundle 9**: Resource links & idempotency
- **Bundle 10**: Subworkflows
- **Bundle 11**: Webhook triggers
- **Bundle 12**: Schedule triggers
- **Bundle 13**: Document templates
- **Bundle 14**: Workflow templates
- **Bundle 15**: Observability (Serilog, OpenTelemetry, Prometheus)
- **Bundle 16**: Background runner (async execution)
- **Bundle 17**: Production hardening (JWT auth, rate limits, caching)

---

## How to Use This Plan

### **For Each Bundle:**

1. **Read the bundle details** (Goal, Reference, Inputs)
2. **Extract spec sections** (use line numbers as guide)
3. **Read existing files** listed in Inputs
4. **Feed to LLM:**
   ```
   I'm implementing Bundle X: [Goal]

   BUNDLE DETAILS:
   [Paste bundle content]

   SPEC REFERENCE:
   [Paste relevant spec sections]

   EXISTING CODE:
   [Paste files listed in Inputs]

   INSTRUCTION:
   Implement all code changes listed in Implementation section.
   Create/modify files exactly as specified.
   No explanation needed, just create the code.
   Output "READY FOR CHECKPOINT" when done.
   ```

5. **Run checkpoint tests** to verify

6. **Commit** with bundle message

7. **Move to next bundle**

---

### **Example Prompt for Bundle 1:**

```
I'm implementing Bundle 1: Minimal E2E Workflow

GOAL: Execute a simple 2-node linear workflow via API, store results in PostgreSQL

BUNDLE DETAILS:
[Paste entire Bundle 1 section from above]

SPEC REFERENCE:
[Paste §3.1 Core Workflow Tables lines 131-207]
[Paste §7.1 Action Contract lines 849-901]
[Paste §9.2 Run Loop lines 1076-1087]

INSTRUCTION:
Create all 24 files listed in the Implementation section.
Use the exact code provided.
After creating all files, output: "READY FOR CHECKPOINT"
```

---

### **Context Management:**

**Per bundle:**
- Bundle details: ~500-1000 lines
- Spec references: ~200-400 lines
- Existing code: ~200-500 lines
- **Total: ~1000-2000 lines per LLM call**

Much more efficient than 160 separate steps!

---

### **Progress Tracking:**

```bash
# .ai-progress
CURRENT_BUNDLE=1
TOTAL_BUNDLES=17
BUNDLES_COMPLETED=0
```

**After each bundle:**
```bash
git log --oneline | grep "Bundle"
# Shows: Bundle 3 complete, Bundle 2 complete, Bundle 1 complete
```

---

## Time Estimates

| Approach | Unit Count | Time/Unit | Total Time |
|----------|------------|-----------|------------|
| v1 (160 steps) | 160 | 5-10 min | 12-26 hours |
| v2 (17 bundles) | 17 | 30-60 min | 8-17 hours |

**With optimizations:**
- Bundles 1-3: 1-2h each (learning)
- Bundles 4-14: 30-60 min each (pattern established)
- Bundles 15-17: 1-2h each (complex)

**Realistic total: 10-15 hours**

---

## When to Use Which Plan

**Use v2 (this plan) when:**
- ✅ You want to move fast
- ✅ You trust the LLM to generate coherent code
- ✅ You want working features quickly
- ✅ You're comfortable debugging larger chunks

**Use v1 (160 steps) when:**
- ✅ You want to learn every detail
- ✅ You want to debug step-by-step
- ✅ You're new to the codebase
- ✅ Something broke and you need to bisect

**Hybrid approach:**
- Use v2 for implementation
- Reference v1 when debugging
- v1 = detailed manual, v2 = quick implementation guide

---

**Ready to start? Begin with Bundle 1!** 🚀

---

**END OF IMPLEMENTATION PLAN V2**
