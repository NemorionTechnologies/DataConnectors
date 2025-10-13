# DataWorkflows Engine — Technical Specification (Rev. 3)

> **Status:** Planning complete → ready for repo scaffolding
>
> **Audience:** .NET 8 architects/engineers, DevOps, SRE
>
> **Goals:** Define an implementation-ready design for a durable, secure, horizontally scalable workflow engine that executes DAG-defined business processes across external systems (Slack, Email, Confluence, Monday, etc.).

---

## 1. Core Concepts & Mission

* **Workflow**: A **Directed Acyclic Graph (DAG)** defined in JSON, validated against a strict JSON Schema. Each workflow version is immutable once published.
* **Node**: A single unit of work implemented by an **Action**.
* **Edge**: A connector with optional **when** and **condition** governing routing.
* **Action**: Reusable C# logic, resolved via DI at runtime (e.g., `monday.get-items`, `slack.post-message`).
* **Context**: Read-only snapshot exposed to conditions/templates, backed by a thread-safe state store populated by action outputs during execution.
* **Conductor**: The orchestrator that validates, plans, runs, and persists workflow execution.

**Mission:** Reliably execute workflow definitions, manage state, enforce conditions, and provide robust logging, observability, and idempotent integrations with external systems.

---

## 2. High-Level Architecture

**Application:** ASP.NET Core (.NET 8), Clean Architecture.

* **API Layer**

  * Versioned endpoints under `/api/v1`.
  * JWT Bearer auth, role/claim-based authorization. (We're going to hold off implementing authentication for a bit)
  * Rate limiting (per-token & per-workflow caps).
  * Requests are **enqueue-only**; the API is stateless.

* **Runner (BackgroundService)**

  * Consumes messages (queue/outbox) and executes workflows.
  * Enforces concurrency, timeouts, retries, and fail-fast policy.

* **Conductor (Orchestrator)**

  * Validates workflow def (JSON Schema + static graph checks).
  * Builds a **superset graph** (all edges, conditions compiled).
  * Executes nodes with bounded parallelism.
  * Produces execution events & context snapshots.

* **Action Registry**

  * Attribute/DI-based discovery of `IWorkflowAction` implementations.
  * Typed param binding (Scriban-rendered JSON → `TParams`).

* **Persistence Layer (Dapper)**

  * Repositories + stored procedures; PostgreSQL as source of truth.

* **Condition Evaluator (Jint)**

  * Sandboxed JS for edge conditions only. Read-only scope.

* **Template Engine (Scriban)**

  * Whitelisted features; strict/timeouted rendering for parameters.

* **Observability**

  * Serilog (structured JSON logs), OpenTelemetry traces/metrics, health checks.

---

## 3. Data Model & SQL (PostgreSQL)

> **Key design changes:** Immutable definitions via `WorkflowDefinitions`, composite idempotency `(WorkflowId, WorkflowRequestId)`, global uniqueness for external resources, explicit FKs, attempt tracking.

### 3.1 Tables

```sql
-- Immutable catalog of workflows (current pointer lives here)
CREATE TABLE IF NOT EXISTS Workflows (
  Id              TEXT PRIMARY KEY,
  DisplayName     TEXT NOT NULL,
  Description     TEXT,
  CurrentVersion  INT,
  IsEnabled       BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UpdatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Immutable versions of a workflow definition
CREATE TABLE IF NOT EXISTS WorkflowDefinitions (
  WorkflowId      TEXT        NOT NULL,
  Version         INT         NOT NULL,
  DefinitionJson  JSONB       NOT NULL,
  Checksum        TEXT        NOT NULL,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (WorkflowId, Version),
  FOREIGN KEY (WorkflowId) REFERENCES Workflows(Id) ON DELETE CASCADE
);

-- Each execution of a specific (WorkflowId, Version)
CREATE TABLE IF NOT EXISTS WorkflowExecutions (
  Id                 UUID PRIMARY KEY,
  WorkflowId         TEXT    NOT NULL,
  WorkflowVersion    INT     NOT NULL,
  WorkflowRequestId  TEXT    NOT NULL, -- external idempotency key
  Status             TEXT    NOT NULL CHECK (Status IN ('Pending','Running','Succeeded','Failed','Cancelled')),
  Priority           INT     NULL,
  TriggerPayloadJson JSONB   NOT NULL,
  ContextSnapshotJson JSONB  NULL,
  StartTime          TIMESTAMPTZ NULL,
  EndTime            TIMESTAMPTZ NULL,
  TenantId           TEXT    NULL,
  CorrelationId      TEXT    NULL,
  FOREIGN KEY (WorkflowId, WorkflowVersion)
    REFERENCES WorkflowDefinitions(WorkflowId, Version) ON DELETE RESTRICT
);

-- Composite idempotency across a workflow
CREATE UNIQUE INDEX IF NOT EXISTS ux_wfexec_workflow_request
  ON WorkflowExecutions(WorkflowId, WorkflowRequestId);

-- Efficient status scans for dashboards
CREATE INDEX IF NOT EXISTS ix_wfexec_active_status
  ON WorkflowExecutions(Status, StartTime DESC)
  WHERE Status IN ('Pending','Running');

-- Individual node/action execution attempts (aggregate row per attempt)
CREATE TABLE IF NOT EXISTS ActionExecutions (
  Id                  UUID PRIMARY KEY,
  WorkflowExecutionId UUID    NOT NULL,
  NodeId              TEXT    NOT NULL,
  ActionType          TEXT    NOT NULL,
  Status              TEXT    NOT NULL CHECK (Status IN ('Succeeded','Failed','RetriableFailure','Skipped')),
  Attempt             INT     NOT NULL DEFAULT 1,
  RetryCount          INT     NOT NULL DEFAULT 0,
  ParametersJson      JSONB   NULL,
  OutputsJson         JSONB   NULL,
  ErrorJson           JSONB   NULL,
  StartTime           TIMESTAMPTZ NULL,
  EndTime             TIMESTAMPTZ NULL,
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_actionexec_by_exec_node
  ON ActionExecutions(WorkflowExecutionId, NodeId);

-- External resources created/linked for idempotency & audit
CREATE TABLE IF NOT EXISTS WorkflowResourceLinks (
  Id                  UUID PRIMARY KEY,
  WorkflowExecutionId UUID NOT NULL,
  ActionExecutionId   UUID NOT NULL,
  SystemName          TEXT NOT NULL,
  ResourceType        TEXT NOT NULL,
  ResourceId          TEXT NOT NULL,
  ExternalUrl         TEXT NULL,
  CreatedAt           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE,
  FOREIGN KEY (ActionExecutionId)   REFERENCES ActionExecutions(Id)   ON DELETE CASCADE
);

-- Global uniqueness to enforce cross-run idempotency
CREATE UNIQUE INDEX IF NOT EXISTS ux_res_unique
  ON WorkflowResourceLinks(SystemName, ResourceType, ResourceId);

CREATE INDEX IF NOT EXISTS ix_res_by_exec
  ON WorkflowResourceLinks(WorkflowExecutionId);

-- Optional: compiled plan cache (for cold-start avoidance)
CREATE TABLE IF NOT EXISTS WorkflowPlans (
  WorkflowId TEXT NOT NULL,
  Version    INT  NOT NULL,
  PlanJson   JSONB NOT NULL,
  CreatedAt  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (WorkflowId, Version),
  FOREIGN KEY (WorkflowId, Version) REFERENCES WorkflowDefinitions(WorkflowId, Version) ON DELETE CASCADE
);

-- Optional: DB-based dead-letter queue
CREATE TABLE IF NOT EXISTS DeadLetters (
  Id            BIGSERIAL PRIMARY KEY,
  EnqueuedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  Reason        TEXT NOT NULL,
  PayloadJson   JSONB NOT NULL,
  Attempts      INT NOT NULL DEFAULT 0,
  LastError     TEXT NULL
);

-- Append-only execution timeline for UI/debugging
CREATE TABLE IF NOT EXISTS ExecutionEvents (
  Id                  BIGSERIAL PRIMARY KEY,
  WorkflowExecutionId UUID NOT NULL,
  Ts                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  Level               TEXT NOT NULL, -- Info, Warn, Error, Debug
  Category            TEXT NOT NULL, -- Orchestrator, Action:slack.post-message, etc.
  Data                JSONB NOT NULL,
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);
```

### 3.2 Stored Procedure Sketches (idempotent patterns)

* `sp_StartWorkflowExecution(workflowId text, workflowRequestId text, triggerJson jsonb)` → `(executionId uuid, wasExisting bool)`
* `sp_RecordActionAttempt(executionId uuid, nodeId text, actionType text, status text, attempt int, retryCount int, params jsonb, outputs jsonb, error jsonb)`
* `sp_LinkExternalResource(executionId uuid, actionExecutionId uuid, system text, type text, resourceId text, externalUrl text)` (conflict → do nothing)
* `sp_CompleteWorkflow(executionId uuid, status text, contextSnapshot jsonb)`

---

## 4. Workflow Definition Schema

> **Edge rules clarified**: edges support `when: success|failure|always` + `condition` (Jint). **Multiple matching edges run in parallel** (intentional). Node-level `onFailure` provides a default failure route if no explicit failure edge exists.

**`workflow.schema.json`**

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DataWorkflows Definition",
  "type": "object",
  "required": ["id", "displayName", "startNode", "nodes"],
  "properties": {
    "id": { "type": "string", "pattern": "^[a-z0-9-]+$" },
    "displayName": { "type": "string" },
    "description": { "type": "string" },
    "startNode": { "type": "string" },
    "nodes": { "type": "array", "items": { "$ref": "#/definitions/Node" } }
  },
  "definitions": {
    "Policies": {
      "type": "object",
      "properties": {
        "timeoutMs": { "type": "integer", "minimum": 1 },
        "retry": {
          "type": "object",
          "properties": {
            "maxAttempts": { "type": "integer", "minimum": 0 },
            "baseDelayMs": { "type": "integer", "minimum": 0 },
            "backoffFactor": { "type": "number", "minimum": 1.0 },
            "jitter": { "type": "boolean" }
          },
          "additionalProperties": false
        }
      },
      "additionalProperties": false
    },
    "Edge": {
      "type": "object",
      "required": ["targetNode"],
      "properties": {
        "targetNode": { "type": "string" },
        "when": { "type": "string", "enum": ["success", "failure", "always"] },
        "condition": { "type": "string" }
      },
      "additionalProperties": false
    },
    "Node": {
      "type": "object",
      "required": ["id", "actionType"],
      "properties": {
        "id": { "type": "string" },
        "actionType": { "type": "string" },
        "parameters": { "type": "object" },
        "onFailure": { "type": "string", "description": "Node ID to execute if this action fails and no explicit failure edge matches." },
        "policies": { "$ref": "#/definitions/Policies" },
        "edges": { "type": "array", "items": { "$ref": "#/definitions/Edge" } }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

### 4.1 Cross-Reference Validation (engine-side)

At load/compile time the engine additionally validates:

* `startNode` exists in `nodes`.
* All `edges[].targetNode` reference existing nodes.
* `onFailure` (if present) references an existing node.
* The superset graph (all edges, conditions ignored) is acyclic and every node is reachable from `startNode`.

### 4.2 Routing Semantics

* **Edge satisfaction** = `when` matches the parent outcome (success/failure, or `always`) **and** `condition` (if present) evaluates **true**.
* **Parallelism by design**: If multiple edges from a node are satisfied, **all target nodes are eligible** and may run in parallel, subject to join rules.
* **Join nodes**: A node with multiple incoming edges executes when **all of its satisfied incoming edges’ parents have finished successfully** (see §6.3). If a parent fails and there is **no failure route** that eventually satisfies the join, the join becomes **unreachable** and is skipped when fail-fast cancels the branch (see §6.4).

---

## 5. Template Engine (Scriban)

**Choice:** **Scriban** (explicit). Render action `parameters` from templates into JSON prior to deserialization into `TParams`.

**Options**

```csharp
public sealed class TemplateEngineOptions {
  public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(2);
  public string NullValueReplacement { get; init; } = ""; // for null → string
  public bool StrictMode { get; init; } = true;            // undefined → error
  public bool EnableLoops { get; init; } = false;          // disabled for safety
  public bool EnableFunctions { get; init; } = false;      // whitelist none (MVP)
}
```

**Sandboxing & Behavior**

* Provide a **small, read-only model**: `{ trigger, context, vars }`.
* Disable includes/files, scripting, and custom functions.
* Rendering timeout enforced by CTS.
* **Nulls & missing members**: with `StrictMode=true`, template errors bubble; caller records `ErrorJson` and applies retry policy if marked retriable.
* Type coercion: stringification via Scriban defaults; date/time formatted by explicit helper functions if later whitelisted.

---

## 6. Condition Evaluator (Jint)

**Options**

```csharp
public sealed class JintConditionEvaluatorOptions {
  public TimeSpan ScriptTimeout { get; init; } = TimeSpan.FromSeconds(2);
  public int MaxStatements { get; init; } = 500;
  public long MemoryLimitBytes { get; init; } = 4 * 1024 * 1024;
  public int MaxRecursionDepth { get; init; } = 10;
}
```

**Sandbox**

* No CLR access; only `{ trigger, context, vars }` POCO snapshots.
* Pre-compile & cache by `(workflowId, version, nodeId, edgeIndex)`.
* Timeouts and statement/memory limits enforced; exceptions recorded in `ExecutionEvents` and treated as **false** (edge not satisfied).

---

## 7. Action Contract & Registry

**Base contract (non-generic)**

```csharp
public interface IWorkflowAction {
  string Type { get; } // e.g., "monday.get-items"
  Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}
```

**Typed convenience**

```csharp
public interface IWorkflowAction<TParams, TOutputs> : IWorkflowAction { }
```

**Execution context & result**

```csharp
public sealed record ActionExecutionContext(
  Guid WorkflowExecutionId,
  string NodeId,
  IReadOnlyDictionary<string, object?> Parameters, // already rendered (Scriban)
  WorkflowReadonlyContext Context,                 // read-only view
  string CorrelationId,
  IServiceProvider Services
);

public enum ActionExecutionStatus { Succeeded, Failed, RetriableFailure, Skipped }

public sealed record ActionExecutionResult(
  ActionExecutionStatus Status,
  IReadOnlyDictionary<string, object?> Outputs,
  IReadOnlyCollection<WorkflowResourceLink> ResourceLinks,
  string? ErrorMessage = null
);

public sealed record WorkflowResourceLink(
  string SystemName,
  string ResourceType,
  string ResourceId,
  string? ExternalUrl = null
);
```

**Registry & DI**

* Actions decorated with `[WorkflowAction("slack.post-message")]` → scanned at startup.
* Parameter binding pipeline: Scriban render → `JsonSerializer.Deserialize<TParams>()` → validate (FluentValidation) → pass to action.

---

## 8. Workflow Context Model

**Thread-safe store with read-only façade for engine consumers**

```csharp
public sealed class WorkflowContext {
  public WorkflowMetadata Metadata { get; }
  public object Trigger { get; } // immutable trigger snapshot
  private readonly ConcurrentDictionary<string, object?> _data = new();

  public void SetActionOutput(string nodeId, object? output) => _data[nodeId] = output;
  public bool TryGetActionOutput<T>(string nodeId, out T? value) {
    if (_data.TryGetValue(nodeId, out var v) && v is not null) { value = (T?)v; return true; }
    value = default; return false;
  }

  public IReadOnlyDictionary<string, object?> Snapshot() => new ReadOnlyDictionary<string, object?>(_data);
}

public sealed class WorkflowReadonlyContext {
  public object Trigger { get; init; } = default!;
  public IReadOnlyDictionary<string, object?> Data { get; init; } = default!; // used by Jint/Scriban
}
```

**Serialization**

* `System.Text.Json` with `JsonSerializerOptions` tuned for:

  * CamelCase, ignore nulls, max depth increased, reference handling `IgnoreCycles`.
* Context snapshot policy: **project/prune** to avoid large blobs (store raw action outputs in `ActionExecutions.OutputsJson`; snapshot contains **summaries/IDs** only when configured).

---

## 9. Orchestrator Execution Model

### 9.1 Planning & Topology

* Load `(WorkflowId, Version)` definition and validate against JSON Schema.
* **Cross-reference checks** (see §4.1).
* Build a **superset graph** including **all** edges (ignore `when`/`condition`).
* Topologically sort the superset to verify **acyclic**; precompute `expectedIncoming[target]` (count of incoming edges from parents reachable from `startNode`).
* Pre-compile Jint conditions & Scriban templates; cache by `(workflowId, version)`.

### 9.2 Run Loop (bounded concurrency)

* Use a process-wide semaphore `MaxParallelActions`.
* Workflow-level CTS with `DefaultWorkflowTimeout`.
* Seed run-queue with nodes of in-degree 0 (or `startNode` and successors if you choose a single-entry requirement).
* For each runnable node:

  1. Render parameters via Scriban → JSON → `TParams` → validate.
  2. Execute action under **per-node timeout** (CTS linked to workflow CTS).
  3. Apply **retry policy** (Polly) on `RetriableFailure` or specific transient exceptions.
  4. Persist `ActionExecutions` attempt with parameters/outputs/error.
  5. `WorkflowContext.SetActionOutput(nodeId, outputs)` (thread-safe).
  6. Evaluate outgoing edges: mark satisfied children; when `inDegreeSatisfied[target] == expectedIncoming[target]` → enqueue child.

### 9.3 Retry Semantics

* Retries re-enter step (1): **parameters are re-rendered** each attempt (so time-based templates can change).
* Fresh CTS per attempt; retry delays **do not** occupy the global semaphore.
* Attempt metadata is persisted (Attempt, RetryCount, ErrorJson). If the runner crashes mid-retry, the next pickup resumes from persisted attempt counts.

### 9.4 Fail-Fast & Joins

* **Sibling branch** = any concurrently eligible children of the **same parent** at the time of fan-out.
* On **unhandled failure** (after retries):

  * Cancel the workflow CTS → actions should honor cancellation; stubborn actions will be allowed to complete up to their timeout; runner marks them `Skipped` if not started.
  * Joins waiting on cancelled branches become **unreachable** and are not scheduled.
* Node-level `onFailure` provides a default route: the engine synthesizes an implicit failure edge from this node to `onFailure` during planning (unless an explicit failure edge exists).

---

## 10. Options

```csharp
public sealed class OrchestrationOptions {
  public int MaxParallelActions { get; init; } = 10;
  public TimeSpan DefaultActionTimeout { get; init; } = TimeSpan.FromMinutes(5);
  public TimeSpan DefaultWorkflowTimeout { get; init; } = TimeSpan.FromHours(1);
  public RetryPolicyOptions RetryPolicy { get; init; } = new();
}

public sealed class RetryPolicyOptions {
  public int MaxRetryAttempts { get; init; } = 3;
  public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);
  public double BackoffFactor { get; init; } = 2.0;
  public bool Jitter { get; init; } = true;
}

public sealed class ErrorHandlingOptions {
  public bool UseDeadLetterQueue { get; init; } = true;
  public string DeadLetterQueueName { get; init; } = "workflow-dlq";
  public int MaxPoisonMessageRetries { get; init; } = 3;
  public TimeSpan PoisonMessageDelay { get; init; } = TimeSpan.FromMinutes(5);
}

public sealed class ScalabilityOptions {
  public int MaxNodesPerWorkflow { get; init; } = 1000;
  public int MaxContextSizeBytes { get; init; } = 10 * 1024 * 1024; // 10MB
  public bool EnableDistributedLocking { get; init; } = false; // e.g., Redis
  public string? RedisConnectionString { get; init; }
}
```

---

## 11. Queueing Model & DLQ

**Primary:** Any durable queue (Azure Service Bus, RabbitMQ, SQS). For infra-minimal MVP, use a **DB Outbox** table + `BackgroundService` polling with SKIP LOCKED.

**Dead-Letter:**

* If deserialization/validation of a trigger fails, or retries of dequeue fail → move to DLQ (`DeadLetters`) with `Reason` and `PayloadJson`.
* Expose admin endpoints to requeue or purge DLQs.

**Concurrency Control:**

* One active runner per `WorkflowExecutionId` enforced via DB compare-and-set (`Status` transition `Pending→Running`) and/or distributed locks.

---

## 12. Authentication, Authorization, Rate Limiting

**JWT**

* Required scopes:

  * `workflows:execute` for `POST /workflows/{id}/execute`
  * `workflows:read` for reads
  * `workflows:manage` for create/update/delete
* Multi-tenant: require `tenant_id` claim; enforce row-level filters by `TenantId` column where applicable.

**Rate Limiting**

* Per-token budget: e.g., 60 req/min burst 120.
* Per-workflow throttle: e.g., N executions/minute per workflow (configurable).
* Responses include standard headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `Retry-After`.

---

## 13. API Specification (v1)

**POST /api/v1/workflows/{workflowId}/execute**

* Triggers a new execution (enqueue-only).
* Request:

```json
{
  "requestId": "slack-event-12345",
  "trigger": { "source": "slack", "boardId": "123", "itemId": "456", "userId": "U123" },
  "priority": 5,
  "tenantId": "acme"
}
```

* Response `202 Accepted`:

```json
{ "executionId": "...", "status": "Pending", "statusUrl": "/api/v1/executions/..." }
```

**GET /api/v1/executions/{executionId}?include=actions,resources,events**

* Returns the execution plus optional expansions.

**GET /api/v1/workflows** / **GET /api/v1/workflows/{id}**

* List definitions; fetch specific version with `?version=`; default is `CurrentVersion`.

**POST /api/v1/workflows**

* Create/update: posts a new version (sets `CurrentVersion`).

**DELETE /api/v1/workflows/{id}**

* Disables workflow (`IsEnabled=false`).

**POST /api/v1/executions/{id}/cancel**

* Best effort cancel via CTS; marks `Cancelled` if runner confirms.

**Admin**: DLQ endpoints to list/requeue/purge dead letters.

**Error Envelope**

```json
{
  "error": {
    "code": "WORKFLOW_NOT_FOUND",
    "message": "Workflow 'test-workflow' does not exist",
    "correlationId": "abc-123",
    "timestamp": "2025-01-15T10:30:00Z"
  }
}
```

---

## 14. Observability & Ops

* **Logging:** Serilog JSON with properties: `CorrelationId`, `WorkflowId`, `WorkflowVersion`, `ExecutionId`, `NodeId`, `ActionType`.
* **Tracing:** `ActivitySource` spans per workflow and per action with tags for retry attempts and status.
* **Metrics (OpenTelemetry/Prometheus):**

  * `workflow_duration_seconds{workflowId,status}` (histogram)
  * `workflow_success_total{workflowId}` / `workflow_failure_total{workflowId}`
  * `action_duration_seconds{actionType}` (histogram)
  * `action_retry_total{actionType}`
  * `queue_depth` (gauge)
* **Health:** `/health/live`, `/health/ready` with DB and queue checks.

---

## 15. Examples

### 15.1 Linear

```json
{
  "id": "get-monday-status-report",
  "displayName": "Get Monday Status Report",
  "startNode": "fetch-monday-items",
  "nodes": [
    {
      "id": "fetch-monday-items",
      "actionType": "monday.get-items",
      "parameters": {
        "boardId": "{{ trigger.boardId }}",
        "filter": { "rules": [{ "column": "Status", "operator": "eq", "value": "In Progress" }] }
      },
      "policies": { "retry": { "maxAttempts": 3, "baseDelayMs": 300, "backoffFactor": 2.0, "jitter": true } },
      "edges": [ { "targetNode": "post-to-slack", "when": "success" } ]
    },
    {
      "id": "post-to-slack",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Found {{ context.data['fetch-monday-items'].items.length }} items in progress."
      }
    }
  ]
}
```

### 15.2 Branching with Failure Route

```json
{
  "id": "onboard-new-project",
  "displayName": "Onboard New Project",
  "startNode": "get-project-item",
  "nodes": [
    {
      "id": "get-project-item",
      "actionType": "monday.get-items",
      "parameters": {
        "boardId": "{{ trigger.boardId }}",
        "filter": { "rules": [{ "column": "Item ID", "operator": "eq", "value": "{{ trigger.itemId }}" }] }
      },
      "edges": [
        { "targetNode": "create-confluence-page", "when": "success", "condition": "context.data['get-project-item'].items[0].Status === 'Approved'" },
        { "targetNode": "notify-not-approved", "when": "success", "condition": "context.data['get-project-item'].items[0].Status !== 'Approved'" }
      ],
      "onFailure": "notify-error"
    },
    {
      "id": "create-confluence-page",
      "actionType": "confluence.create-page",
      "parameters": {
        "space": "PROJECTS",
        "title": "{{ context.data['get-project-item'].items[0].Name }} - Project Brief"
      }
    },
    {
      "id": "notify-not-approved",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Project '{{ context.data['get-project-item'].items[0].Name }}' is not 'Approved'."
      }
    },
    {
      "id": "notify-error",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Onboarding failed at {{ context.data['get-project-item'].errorNode ?? 'unknown' }}."
      }
    }
  ]
}
```

---

## 16. Testing Strategy

* **Unit**

  * Graph validator: cycles, unreachable nodes, invalid refs.
  * Edge logic: `when`/`condition` permutations, join readiness.
  * Template failures: strict nulls/undefined → error paths & retries.
  * Jint timeouts/memory caps → edge evaluates false, events recorded.

* **Integration**

  * Repos against Postgres via Testcontainers.
  * Runner with MockHttp for connectors; verify retries, backoff, fail-fast, resource link idempotency (unique wins, duplicate reads existing).

* **End-to-End**

  * Full DAG with parallel branches; induce a transient failure to test retry; induce a permanent failure to test cancel + join skip.

* **Perf**

  * Large DAGs (N=1k nodes) fan-out/fan-in; measure CPU/mem; tune `MaxParallelActions`.

---

## 17. Open Questions & Defaults

1. **In-flight version upgrades**: Default = in-flight executions continue on their original `(WorkflowId, Version)`.
2. **Trigger shape**: Default free-form; per-workflow **optional** JSON schema can be supplied and validated at enqueue time.
3. **`core.condition` action**: **Removed** in favor of edge-level conditions; retains simplicity and one mechanism.
4. **Who writes `WorkflowResourceLinks`?** Actions return `ResourceLinks`; the **orchestrator persists** them after a `Succeeded` result.
5. **Idempotency checks in actions**: First consult `WorkflowResourceLinks` (by `(SystemName, ResourceType, ResourceId)`), then optional connector-side lookup.

---

## 18. Next Steps (Implementation Order)

1. Schema migrations for tables/indexes above.
2. JSON Schema (+ engine-side cross-reference validator).
3. Action registry + parameter binding (Scriban) + validators.
4. Queue/outbox + BackgroundService Runner.
5. Conductor (plan cache, execution loop, retries, fail-fast, joins).
6. Minimal action pack: `core.echo` (test), `slack.post-message`, `monday.get-items`.
7. Observability wiring (Serilog + OTel) & health checks.
8. Admin/API endpoints (manage/list/cancel, DLQ ops).

---

**This document supersedes Rev. 2 and incorporates prior gaps/feedback, including:**

* Immutable versioning (`WorkflowDefinitions`), composite idempotency, FKs.
* Correct JSON Schema `$schema` URL; removed legacy `onSuccess` dependency rule.
* Clarified edge semantics (`when` + `condition`) and parallel routing.
* Deterministic planning on superset graph; runtime pruning.
* Join semantics + fail-fast/cancellation.
* Explicit `IWorkflowAction` contract, `WorkflowContext` thread-safety.
* Template/condition engine choices and guardrails.
* Retry/delay behavior, persistence of attempts, resume on crash.
* DLQ and queue model; API surface; authz/rate limits; scalability options.