# DataWorkflows Engine — Technical Specification

> **Status:** Authoritative specification (consolidates Rev 5 + Rev 6)
>
> **Audience:** .NET 8 architects/engineers, DevOps, SRE
>
> **Goals:** Define an implementation-ready design for a durable, secure, horizontally scalable workflow engine that executes DAG-defined business processes across external systems (Monday, Slack, Confluence, Email, etc.), with comprehensive action catalog, trigger system, and subworkflow support.

---

## 1. Core Concepts & Mission

### 1.1 Core Entities

* **Workflow**: A **Directed Acyclic Graph (DAG)** defined in JSON, validated against a strict JSON Schema. Each workflow version is immutable once published.
* **Node**: A single unit of work, either an **Action** or a **Subworkflow** invocation.
* **Edge**: A connector with optional **when** (success/failure/always) and **condition** (Jint JS expression) governing routing.
* **Action**: Reusable C# logic resolved via DI at runtime (e.g., `monday.get-items`, `slack.post-message`). Actions are registered in the ActionCatalog.
* **Subworkflow**: A node that invokes another workflow as a child execution, with optional wait-for-completion semantics.
* **Context**: Read-only snapshot exposed to conditions/templates, backed by a thread-safe state store populated by action outputs during execution.
* **Conductor**: The orchestrator that validates, plans, runs, and persists workflow execution.
* **Trigger**: A mechanism (webhook, schedule, event, manual) that initiates workflow execution.
* **Connector**: A logical grouping of related actions (e.g., Monday connector provides monday.get-items, monday.update-item, etc.).

### 1.2 Design Principles

* **Declarative definitions**: JSON-based, schema-validated workflows
* **Idempotency**: RequestId enforcement, resource link tracking, safe retries
* **Extensibility**: New actions auto-registered via reflection; workflows compose via subworkflows and templates
* **Multi-tenancy**: Tenant isolation across workflows, triggers, connectors, and executions
* **Observability**: Rich metrics, events, and execution logs
* **Immutability**: Published workflow versions are immutable; draft workflows are mutable until published

**Mission:** Reliably execute workflow definitions, manage state, enforce conditions, provide robust logging, observability, and idempotent integrations with external systems.

---

## 2. High-Level Architecture

**Application:** ASP.NET Core (.NET 8), Clean Architecture.

### 2.1 Components

* **API Layer**
  * Versioned endpoints under `/api/v1`.
  * JWT Bearer auth, role/claim-based authorization (stubbed for MVP, not enforced initially).
  * Rate limiting (per-token & per-workflow caps).
  * Requests are **enqueue-only**; the API is stateless.

* **Runner (BackgroundService)**
  * Consumes messages (queue/outbox) and executes workflows.
  * Enforces concurrency, timeouts, retries, and fail-fast policy.
  * Supports distributed deployment (multiple runners with DB-based coordination).

* **Conductor (Orchestrator)**
  * Validates workflow def (JSON Schema + static graph checks).
  * Builds a **superset graph** (all edges, conditions compiled).
  * Executes nodes with bounded parallelism.
  * Produces execution events & context snapshots.
  * Handles subworkflow invocation and hierarchy tracking.

* **Action Registry**
  * Attribute/DI-based discovery of `IWorkflowAction` implementations.
  * Typed param binding (Scriban-rendered JSON → `TParams`).
  * Integration with ActionCatalog for metadata and validation.

* **Catalog System**
  * **Connectors**: Registry of available integrations (Monday, Slack, etc.).
  * **ActionCatalog**: Metadata, schemas, and documentation for all actions.
  * **WorkflowTemplates**: Pre-built workflow definitions for common scenarios.

* **Trigger System**
  * **Webhook**: Auto-creates HTTP endpoints per trigger.
  * **Schedule**: Quartz.NET or Hangfire-based scheduling.
  * **Event**: Integration with internal event bus.
  * **Manual**: Direct API invocation.

* **Persistence Layer (Dapper)**
  * Repositories + stored procedures; PostgreSQL as source of truth.

* **Condition Evaluator (Jint)**
  * Sandboxed JS for edge conditions only. Read-only scope.

* **Template Engine (Scriban)**
  * Whitelisted features; strict/timeouted rendering for parameters.

* **Observability**
  * Serilog (structured JSON logs), OpenTelemetry traces/metrics, health and readiness checks.

---

## 3. Data Model & SQL (PostgreSQL)

### 3.0 Workflow Lifecycle & State Management

Workflows follow a strict lifecycle with three states:

* **Draft**: Mutable, not executable by default (controlled by `WorkflowCatalogOptions.AllowDraftExecution`)
  * Can be edited, validated, tested
  * No immutable version created yet
  * `IsEnabled` is ignored in Draft state

* **Active**: Immutable, executable, triggers can fire
  * Publishing a Draft creates a new immutable `WorkflowDefinitions` entry
  * `CurrentVersion` incremented (if definition changed)
  * `IsEnabled=true` allows execution
  * Transition: `Draft → Active` via `POST /workflows/{id}/publish`

* **Archived**: Immutable, read-only, not executable
  * `IsEnabled=false` prevents new executions
  * In-flight executions continue
  * Pending executions rejected by runner
  * Triggers disabled (not deleted)
  * Transition: `Active → Archived` via `POST /workflows/{id}/archive`

**State transition rules:**
```
Draft → Active     (publish)
Active → Archived  (archive)
Archived → Active  (reactivate, optional)
```

**Immutability enforcement:**
* Only `Draft` workflows can be edited
* Publishing creates a new `WorkflowDefinitions` row with incremented version
* `Active` and `Archived` workflow definitions are read-only
* Checksum validation prevents accidental overwrites

### 3.1 Core Workflow Tables

```sql
-- Immutable catalog of workflows (current pointer lives here)
CREATE TABLE IF NOT EXISTS Workflows (
  Id              TEXT PRIMARY KEY,          -- e.g., "onboard-project"
  DisplayName     TEXT NOT NULL,
  Description     TEXT,
  CurrentVersion  INT,
  Status          TEXT NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Active','Archived')),
  IsEnabled       BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UpdatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Immutable versions of a workflow definition
CREATE TABLE IF NOT EXISTS WorkflowDefinitions (
  WorkflowId      TEXT        NOT NULL,
  Version         INT         NOT NULL,
  DefinitionJson  JSONB       NOT NULL,
  Checksum        TEXT        NOT NULL,      -- SHA256 of DefinitionJson
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (WorkflowId, Version),
  CONSTRAINT uq_workflow_checksum UNIQUE (WorkflowId, Checksum),
  FOREIGN KEY (WorkflowId) REFERENCES Workflows(Id) ON DELETE CASCADE
);

-- Each execution of a specific (WorkflowId, Version)
CREATE TABLE IF NOT EXISTS WorkflowExecutions (
  Id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowId            TEXT    NOT NULL,
  WorkflowVersion       INT     NOT NULL,
  WorkflowRequestId     TEXT    NOT NULL,       -- external idempotency key; API generates a GUID if not provided
  Status                TEXT    NOT NULL CHECK (Status IN ('Pending','Running','Succeeded','Failed','Cancelled')),
  Priority              INT     NULL,
  TriggerPayloadJson    JSONB   NOT NULL,
  ContextSnapshotJson   JSONB   NULL,
  StartTime             TIMESTAMPTZ NULL,
  EndTime               TIMESTAMPTZ NULL,
  TenantId              TEXT    NULL,
  CorrelationId         TEXT    NULL,
  ParentExecutionId     UUID    NULL,           -- for subworkflow tracking
  RequesterUserId       TEXT    NULL,           -- principal who initiated execution
  RequesterEmail        TEXT    NULL,
  RequesterDisplayName  TEXT    NULL,
  FOREIGN KEY (WorkflowId, WorkflowVersion)
    REFERENCES WorkflowDefinitions(WorkflowId, Version) ON DELETE RESTRICT,
  FOREIGN KEY (ParentExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

-- Composite idempotency across a workflow
CREATE UNIQUE INDEX IF NOT EXISTS ux_wfexec_workflow_request
  ON WorkflowExecutions(WorkflowId, WorkflowRequestId);

-- Efficient status scans for dashboards
CREATE INDEX IF NOT EXISTS ix_wfexec_active_status
  ON WorkflowExecutions(Status, StartTime DESC)
  WHERE Status IN ('Pending','Running');

-- Individual node/action execution attempts
CREATE TABLE IF NOT EXISTS ActionExecutions (
  Id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowExecutionId UUID    NOT NULL,
  NodeId              TEXT    NOT NULL,
  ActionType          TEXT    NOT NULL,
  Status              TEXT    NOT NULL CHECK (Status IN ('Succeeded','Failed','RetriableFailure','Skipped')),
  Attempt             INT     NOT NULL DEFAULT 1,  -- 1-based attempt number
  RetryCount          INT     NOT NULL DEFAULT 0,  -- retries before this attempt
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
  Id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
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
```

### 3.2 Execution Hierarchy (Subworkflows)

```sql
CREATE TABLE IF NOT EXISTS WorkflowExecutionHierarchy (
  ParentExecutionId UUID NOT NULL,
  ChildExecutionId  UUID NOT NULL,
  ParentNodeId      TEXT NOT NULL,           -- node ID in parent that launched child
  CreatedAt         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (ParentExecutionId, ChildExecutionId),
  FOREIGN KEY (ParentExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE,
  FOREIGN KEY (ChildExecutionId)  REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_hierarchy_parent ON WorkflowExecutionHierarchy(ParentExecutionId);
CREATE INDEX IF NOT EXISTS ix_hierarchy_child ON WorkflowExecutionHierarchy(ChildExecutionId);
```

### 3.3 Execution Events & Observability

```sql
-- Append-only execution timeline for UI/debugging
CREATE TABLE IF NOT EXISTS ExecutionEvents (
  Id                  BIGSERIAL PRIMARY KEY,
  WorkflowExecutionId UUID NOT NULL,
  Ts                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  Level               TEXT NOT NULL,         -- Info, Warn, Error, Debug
  Category            TEXT NOT NULL,         -- Orchestrator, Action:system.operation, etc.
  Data                JSONB NOT NULL,
  FOREIGN KEY (WorkflowExecutionId) REFERENCES WorkflowExecutions(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_exec_events_by_wfexec
  ON ExecutionEvents(WorkflowExecutionId, Ts DESC);
```

### 3.4 Planning & Dead Letter Queue

```sql
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
```

### 3.5 Catalog Tables

```sql
-- Connectors: logical grouping of actions
CREATE TABLE IF NOT EXISTS Connectors (
  Id                TEXT PRIMARY KEY,
  DisplayName       TEXT NOT NULL,
  Description       TEXT NOT NULL,
  IconUrl           TEXT,
  DocumentationUrl  TEXT,
  IsEnabled         BOOLEAN NOT NULL DEFAULT TRUE,
  RequiresAuth      BOOLEAN NOT NULL DEFAULT TRUE,
  AuthType          TEXT,                    -- OAuth2, ApiKey, Basic, etc.
  ConfigSchema      JSONB,                   -- JSON Schema for connector config
  Capabilities      TEXT[] NOT NULL,
  Version           TEXT NOT NULL,
  CreatedAt         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ActionCatalog: metadata for all available actions
CREATE TABLE IF NOT EXISTS ActionCatalog (
  Id                TEXT PRIMARY KEY,        -- e.g., "monday.get-items" or "monday.get-items-v2"
  DisplayName       TEXT NOT NULL,
  Description       TEXT NOT NULL,
  Category          TEXT NOT NULL,           -- Data, Communication, Document, etc.
  ConnectorId       TEXT NOT NULL REFERENCES Connectors(Id),
  ParameterSchema   JSONB NOT NULL,          -- JSON Schema for parameters
  OutputSchema      JSONB NOT NULL,          -- JSON Schema for outputs
  IsEnabled         BOOLEAN NOT NULL DEFAULT TRUE,
  Version           TEXT NOT NULL,           -- Semantic version: MAJOR.MINOR.PATCH
  DeprecationNotice TEXT NULL,               -- Migration instructions if deprecated
  Tags              TEXT[],
  CreatedAt         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_actions_by_connector ON ActionCatalog(ConnectorId);
CREATE INDEX IF NOT EXISTS ix_actions_enabled ON ActionCatalog(IsEnabled) WHERE IsEnabled = TRUE;
```

### 3.5.1 Template Ecosystem Overview

The engine supports **two distinct template systems** that serve different purposes:

#### **Workflow Templates** (§3.6)
* **Purpose**: Bootstrap new workflows from pre-built patterns
* **Usage**: Design-time / authoring
* **Lifecycle**:
  1. Admin/developer creates WorkflowTemplate with `DefinitionJson` (workflow DAG) and `ConfigSchema` (parameters)
  2. User calls `POST /templates/workflows/{id}/instantiate` with config values
  3. System renders template → creates new Draft workflow in `Workflows` table
  4. User can edit the draft, then publish to Active
* **Examples**: "Monday → Slack Status Report", "Onboard New Project", "Daily Standup Reminder"
* **Storage**: `WorkflowTemplates` table
* **Not executed directly**: Templates become workflows, which are then executed

#### **Document Templates** (§3.8)
* **Purpose**: Generate documents (Confluence pages, emails, reports) at runtime
* **Usage**: Runtime / execution
* **Lifecycle**:
  1. Admin creates TemplateDefinition with `BodyTemplate` (Scriban markdown/HTML) and `FieldsJson` (form schema)
  2. Workflow uses `core.template.render` action with `answers` (form data)
  3. Action renders Scriban template → outputs `{ body, metadata }`
  4. Downstream action (e.g., `confluence.create-page`) uses rendered body
* **Examples**: "Project Brief Template", "Sprint Report Template", "Meeting Notes Template"
* **Storage**: `TemplateSets` + `TemplateDefinitions` tables
* **Consumed by actions**: Not workflows themselves, but data passed between nodes

**Key distinction**: Workflow templates are **meta-workflows** (patterns for creating workflows), while document templates are **runtime data** (consumed during workflow execution).

### 3.6 Workflow Templates

```sql
-- Pre-built workflow templates
CREATE TABLE IF NOT EXISTS WorkflowTemplates (
  Id              TEXT PRIMARY KEY,
  DisplayName     TEXT NOT NULL,
  Description     TEXT NOT NULL,
  Category        TEXT NOT NULL,
  DefinitionJson  JSONB NOT NULL,            -- template workflow definition
  ConfigSchema    JSONB NOT NULL,            -- schema for template instantiation parameters
  ThumbnailUrl    TEXT,
  Author          TEXT,
  IsOfficial      BOOLEAN NOT NULL DEFAULT FALSE,
  Tags            TEXT[],
  CreatedAt       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_templates_category ON WorkflowTemplates(Category);
CREATE INDEX IF NOT EXISTS ix_templates_official ON WorkflowTemplates(IsOfficial) WHERE IsOfficial = TRUE;
```

### 3.7 Triggers

```sql
-- Workflow triggers
CREATE TABLE IF NOT EXISTS WorkflowTriggers (
  Id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  WorkflowId          TEXT NOT NULL REFERENCES Workflows(Id) ON DELETE CASCADE,
  TriggerType         TEXT NOT NULL CHECK (TriggerType IN ('Webhook','Schedule','Event','Manual')),
  IsEnabled           BOOLEAN NOT NULL DEFAULT TRUE,
  ConfigJson          JSONB NOT NULL,        -- schedule cron, webhook path, event filters
  ActivationCondition TEXT,                  -- optional Jint expression
  CreatedAt           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_triggers_workflow ON WorkflowTriggers(WorkflowId);
CREATE INDEX IF NOT EXISTS ix_triggers_type_enabled ON WorkflowTriggers(TriggerType, IsEnabled) WHERE IsEnabled = TRUE;
```

### 3.8 Template Catalog (Document Templates)

```sql
-- Template sets (for document generation)
CREATE TABLE IF NOT EXISTS TemplateSets (
  Id           TEXT PRIMARY KEY,             -- e.g., "project-brief"
  DisplayName  TEXT NOT NULL,
  Team         TEXT NOT NULL,                -- owner/team label
  TenantId     TEXT NULL,                    -- NULL = global/shared
  IsEnabled    BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UpdatedAt    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Template definitions (versioned document templates)
CREATE TABLE IF NOT EXISTS TemplateDefinitions (
  TemplateSetId TEXT NOT NULL,
  Id            TEXT NOT NULL,               -- template key within the set
  Version       INT  NOT NULL,
  DisplayName   TEXT NOT NULL,
  FieldsJson    JSONB NOT NULL,              -- drives intake UI
  BodyTemplate  TEXT NOT NULL,               -- Scriban text (Markdown/HTML)
  HelpMarkdown  TEXT NULL,
  IsEnabled     BOOLEAN NOT NULL DEFAULT TRUE,
  CreatedAt     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (TemplateSetId, Id, Version),
  FOREIGN KEY (TemplateSetId) REFERENCES TemplateSets(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_tmpl_by_set_enabled ON TemplateDefinitions(TemplateSetId, Id, IsEnabled);
```

### 3.9 Stored Procedures

#### sp_StartWorkflowExecution
```sql
CREATE OR REPLACE FUNCTION sp_StartWorkflowExecution(
  p_workflowId TEXT,
  p_requestId TEXT,
  p_triggerJson JSONB,
  p_version INT DEFAULT NULL,
  p_tenantId TEXT DEFAULT NULL,
  p_correlationId TEXT DEFAULT NULL,
  p_parentExecutionId UUID DEFAULT NULL
)
RETURNS TABLE(execution_id UUID, was_existing BOOLEAN)
LANGUAGE plpgsql
AS $$
DECLARE
  v_executionId UUID;
  v_version INT;
  v_isEnabled BOOLEAN;
  v_status TEXT;
  v_existing BOOLEAN;
BEGIN
  -- Check if requestId already exists for this workflow
  SELECT Id INTO v_executionId
  FROM WorkflowExecutions
  WHERE WorkflowId = p_workflowId AND WorkflowRequestId = p_requestId;

  IF FOUND THEN
    -- Idempotent return
    RETURN QUERY SELECT v_executionId, TRUE;
    RETURN;
  END IF;

  -- Check if requestId exists for a different workflow
  IF EXISTS (SELECT 1 FROM WorkflowExecutions WHERE WorkflowRequestId = p_requestId AND WorkflowId != p_workflowId) THEN
    RAISE EXCEPTION 'REQUEST_ID_CONFLICT_OTHER_WORKFLOW' USING ERRCODE = 'WFENG001';
  END IF;

  -- Get current version, status, and enabled flag
  SELECT CurrentVersion, Status, IsEnabled INTO v_version, v_status, v_isEnabled
  FROM Workflows
  WHERE Id = p_workflowId;

  IF NOT FOUND THEN
    RAISE EXCEPTION 'Workflow not found: %', p_workflowId;
  END IF;

  -- Check workflow is Active
  IF v_status != 'Active' THEN
    RAISE EXCEPTION 'Workflow is not Active (current status: %): %', v_status, p_workflowId;
  END IF;

  IF NOT v_isEnabled THEN
    RAISE EXCEPTION 'Workflow is disabled: %', p_workflowId;
  END IF;

  IF p_version IS NOT NULL THEN
    v_version := p_version;
  END IF;

  -- Create new execution
  v_executionId := gen_random_uuid();

  INSERT INTO WorkflowExecutions (
    Id, WorkflowId, WorkflowVersion, WorkflowRequestId,
    Status, TriggerPayloadJson, TenantId, CorrelationId, ParentExecutionId
  ) VALUES (
    v_executionId, p_workflowId, v_version, p_requestId,
    'Pending', p_triggerJson, p_tenantId, p_correlationId, p_parentExecutionId
  );

  RETURN QUERY SELECT v_executionId, FALSE;
END;
$$;
```

#### sp_TryAcquireExecution
```sql
CREATE OR REPLACE FUNCTION sp_TryAcquireExecution(p_executionId UUID)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
DECLARE
  v_acquired BOOLEAN;
BEGIN
  UPDATE WorkflowExecutions
  SET Status = 'Running', StartTime = NOW()
  WHERE Id = p_executionId AND Status = 'Pending';

  GET DIAGNOSTICS v_acquired = ROW_COUNT;
  RETURN v_acquired > 0;
END;
$$;
```

#### sp_CompleteWorkflow
```sql
CREATE OR REPLACE FUNCTION sp_CompleteWorkflow(
  p_executionId UUID,
  p_status TEXT,
  p_contextSnapshot JSONB
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
  v_currentStatus TEXT;
BEGIN
  SELECT Status INTO v_currentStatus
  FROM WorkflowExecutions
  WHERE Id = p_executionId;

  IF NOT FOUND THEN
    RAISE EXCEPTION 'Execution not found: %', p_executionId;
  END IF;

  -- Validate legal transitions
  IF v_currentStatus != 'Running' THEN
    IF v_currentStatus = p_status THEN
      -- Idempotent: already completed with same status
      RETURN;
    END IF;
    RAISE EXCEPTION 'ILLEGAL_STATE_TRANSITION from % to %', v_currentStatus, p_status
      USING ERRCODE = 'WFENG002';
  END IF;

  IF p_status NOT IN ('Succeeded', 'Failed', 'Cancelled') THEN
    RAISE EXCEPTION 'Invalid completion status: %', p_status;
  END IF;

  UPDATE WorkflowExecutions
  SET Status = p_status, EndTime = NOW(), ContextSnapshotJson = p_contextSnapshot
  WHERE Id = p_executionId;
END;
$$;
```

#### sp_LinkExternalResource
```sql
CREATE OR REPLACE FUNCTION sp_LinkExternalResource(
  p_executionId UUID,
  p_actionExecutionId UUID,
  p_system TEXT,
  p_type TEXT,
  p_resourceId TEXT,
  p_externalUrl TEXT
)
RETURNS TEXT
LANGUAGE plpgsql
AS $$
DECLARE
  v_existingExecutionId UUID;
  v_linkId UUID;
BEGIN
  -- Check if resource already linked
  SELECT WorkflowExecutionId INTO v_existingExecutionId
  FROM WorkflowResourceLinks
  WHERE SystemName = p_system AND ResourceType = p_type AND ResourceId = p_resourceId;

  IF FOUND THEN
    IF v_existingExecutionId = p_executionId THEN
      RETURN 'exists_same_execution';
    ELSE
      RAISE EXCEPTION 'RESOURCE_LINK_CONFLICT_OTHER_EXECUTION: % % % already linked to execution %',
        p_system, p_type, p_resourceId, v_existingExecutionId
        USING ERRCODE = 'WFENG003';
    END IF;
  END IF;

  v_linkId := gen_random_uuid();

  INSERT INTO WorkflowResourceLinks (
    Id, WorkflowExecutionId, ActionExecutionId,
    SystemName, ResourceType, ResourceId, ExternalUrl
  ) VALUES (
    v_linkId, p_executionId, p_actionExecutionId,
    p_system, p_type, p_resourceId, p_externalUrl
  );

  RETURN 'created';
END;
$$;
```

#### sp_CheckResourceExists
```sql
CREATE OR REPLACE FUNCTION sp_CheckResourceExists(
  p_system TEXT,
  p_type TEXT,
  p_resourceId TEXT
)
RETURNS TABLE(exists BOOLEAN, execution_id UUID)
LANGUAGE plpgsql
AS $$
BEGIN
  RETURN QUERY
  SELECT TRUE, WorkflowExecutionId
  FROM WorkflowResourceLinks
  WHERE SystemName = p_system AND ResourceType = p_type AND ResourceId = p_resourceId
  LIMIT 1;

  IF NOT FOUND THEN
    RETURN QUERY SELECT FALSE, NULL::UUID;
  END IF;
END;
$$;
```

---

## 4. Workflow Definition Schema

### 4.1 Complete JSON Schema

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
    "triggerSchema": {
      "type": "object",
      "description": "Optional JSON Schema to validate trigger payload"
    },
    "nodes": {
      "type": "array",
      "items": { "$ref": "#/definitions/Node" }
    }
  },
  "definitions": {
    "Policies": {
      "type": "object",
      "properties": {
        "timeoutMs": { "type": "integer", "minimum": 1 },
        "rerenderOnRetry": {
          "type": "boolean",
          "default": false,
          "description": "If true, parameters re-render on each retry; if false, first render is reused"
        },
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
        "when": {
          "type": "string",
          "enum": ["success", "failure", "always"],
          "default": "success"
        },
        "condition": {
          "type": "string",
          "description": "Jint JavaScript expression evaluated in context scope"
        }
      },
      "additionalProperties": false
    },
    "Node": {
      "type": "object",
      "required": ["id"],
      "properties": {
        "id": { "type": "string" },
        "nodeType": {
          "type": "string",
          "enum": ["action", "subworkflow"],
          "default": "action"
        },
        "actionType": {
          "type": "string",
          "description": "Required if nodeType=action; e.g., 'monday.get-items'"
        },
        "workflowId": {
          "type": "string",
          "description": "Required if nodeType=subworkflow; the workflow to invoke"
        },
        "workflowVersion": {
          "type": "integer",
          "description": "Optional; if omitted, uses CurrentVersion"
        },
        "waitForCompletion": {
          "type": "boolean",
          "default": true,
          "description": "If true, blocks until subworkflow completes; if false, fire-and-forget"
        },
        "parameters": {
          "type": "object",
          "description": "Scriban-templated parameters passed to action or subworkflow"
        },
        "onFailure": {
          "type": "string",
          "description": "Node ID to execute if this action fails and no explicit failure edge matches"
        },
        "routePolicy": {
          "type": "string",
          "enum": ["parallel", "firstMatch"],
          "default": "parallel",
          "description": "parallel: all satisfied edges run; firstMatch: first satisfied edge only"
        },
        "policies": { "$ref": "#/definitions/Policies" },
        "edges": {
          "type": "array",
          "items": { "$ref": "#/definitions/Edge" }
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

### 4.2 Cross-Reference Validation (Engine-Side)

At load/compile time the engine additionally validates:

* `startNode` exists in `nodes`
* All `edges[].targetNode` reference existing nodes
* `onFailure` (if present) references an existing node
* For nodes with `nodeType=action`: `actionType` is required
* For nodes with `nodeType=subworkflow`: `workflowId` is required
* The superset graph (all edges, conditions ignored) is acyclic
* Every node is reachable from `startNode` in the superset graph

### 4.3 Routing Semantics

* **Edge satisfaction** = `when` matches the parent outcome (success/failure, or `always`) **and** `condition` (if present) evaluates **true**
* **Parallelism by design** (`routePolicy=parallel`): If multiple edges from a node are satisfied, **all target nodes are eligible** and may run in parallel, subject to join rules
* **First-match routing** (`routePolicy=firstMatch`): Edges evaluated in array order; only the **first satisfied** edge is taken; remaining edges are not evaluated
* **Join nodes**: A node with multiple incoming edges executes when **all of its satisfied incoming edges' parents have finished successfully**. Parents whose edges were unsatisfied are excluded from the join count.

---

## 5. Template Engine (Scriban)

### 5.1 Configuration

```csharp
public sealed class TemplateEngineOptions {
  public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(2);
  public string NullValueReplacement { get; init; } = "";  // for null → string
  public bool StrictMode { get; init; } = true;            // undefined → error
  public bool EnableLoops { get; init; } = false;          // disabled for safety
  public bool EnableFunctions { get; init; } = false;      // whitelist none (MVP)
}
```

### 5.2 Sandboxing & Behavior

* Provide a **small, read-only model**: `{ trigger, context, vars }`
* Disable includes/files, scripting, and custom functions
* Rendering timeout enforced by CancellationTokenSource
* **Nulls & missing members**: with `StrictMode=true`, template errors bubble; caller records `ErrorJson` and applies retry policy if marked retriable
* Type coercion: stringification via Scriban defaults; date/time formatted by explicit helper functions if later whitelisted

### 5.3 Usage in Actions

* Action parameters are Scriban templates rendered before execution
* Rendered JSON is deserialized to typed `TParams`
* If `rerenderOnRetry=false` (default), first rendered JSON is persisted and reused on retries
* If `rerenderOnRetry=true`, parameters are re-rendered each attempt

### 5.4 Document Template Rendering

* The `core.template.render` action loads templates from TemplateDefinitions
* Uses same Scriban engine with same security constraints
* Inputs validated against `FieldsJson.validation.jsonSchema`

---

## 6. Condition Evaluator (Jint)

### 6.1 Configuration

```csharp
public sealed class JintConditionEvaluatorOptions {
  public TimeSpan ScriptTimeout { get; init; } = TimeSpan.FromSeconds(2);
  public int MaxStatements { get; init; } = 500;
  public long MemoryLimitBytes { get; init; } = 4 * 1024 * 1024;
  public int MaxRecursionDepth { get; init; } = 10;
}
```

### 6.2 Sandbox

* No CLR access; only `{ trigger, context, vars }` POCO snapshots
* Pre-compile & cache by `(workflowId, version, nodeId, edgeIndex)`
* Timeouts and statement/memory limits enforced
* Exceptions recorded in `ExecutionEvents` and treated as **false** (edge not satisfied)

### 6.3 Trigger Activation Conditions

* WorkflowTriggers can specify optional `ActivationCondition` (Jint expression)
* Evaluated before workflow execution enqueues
* If false, trigger event is logged but workflow is not started

---

## 7. Action Contract & Registry

### 7.1 Base Contract

```csharp
public interface IWorkflowAction {
  string Type { get; } // e.g., "monday.get-items"
  Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}
```

### 7.2 Typed Convenience

```csharp
public interface IWorkflowAction<TParams, TOutputs> : IWorkflowAction { }
```

### 7.3 Execution Context

```csharp
public sealed record ActionExecutionContext(
  Guid WorkflowExecutionId,
  string NodeId,
  object TypedParameters,                                // pre-deserialized TParams
  IReadOnlyDictionary<string, object?> RawParameters,    // rendered JSON → dict (audit)
  WorkflowReadonlyContext Context,
  string CorrelationId,
  IServiceProvider Services
) {
  public T GetParameters<T>() => (T)TypedParameters;
}
```

### 7.4 Execution Result

```csharp
public enum ActionExecutionStatus {
  Succeeded,          // Action completed successfully
  Failed,             // Permanent failure, do not retry
  RetriableFailure,   // Transient failure, retry if policy allows
  Skipped             // Node was skipped (cancelled workflow, condition false, etc.)
}

public sealed record ActionExecutionResult(
  ActionExecutionStatus Status,
  IReadOnlyDictionary<string, object?> Outputs,
  IReadOnlyCollection<WorkflowResourceLink> ResourceLinks,
  string? ErrorMessage = null
);

public sealed record WorkflowResourceLink(
  string SystemName,      // e.g., "monday", "slack"
  string ResourceType,    // e.g., "item", "message", "page"
  string ResourceId,      // external ID
  string? ExternalUrl = null
);
```

### 7.5 Registry & DI

* Actions decorated with `[WorkflowAction("slack.post-message")]`
* Scanned at startup via reflection
* Registered in DI container as scoped services
* Parameter binding pipeline:
  1. Scriban render (with context)
  2. `JsonSerializer.Deserialize<TParams>()`
  3. FluentValidation validate
  4. Pass to action `ExecuteAsync`

### 7.6 Action Catalog Integration

* On startup, scan for `IWorkflowAction` implementations
* Extract metadata from attributes and schemas
* Upsert to ActionCatalog table
* Catalog provides:
  * Parameter schema (for UI builders)
  * Output schema (for downstream node validation)
  * Documentation, tags, versioning

#### Action Versioning Policy

Actions follow **semantic versioning** with the following rules:

* **Version format**: `MAJOR.MINOR.PATCH` (e.g., `1.2.0`)
* **Patch changes** (bug fixes, non-breaking): Same action ID, increment patch
  * Example: `monday.get-items` v1.0.0 → v1.0.1
  * Workflows automatically use latest patch within their major.minor
* **Minor changes** (new optional parameters, new outputs): Same action ID, increment minor
  * Example: `monday.get-items` v1.0.1 → v1.1.0
  * Backwards compatible; existing workflows continue to work
* **Major changes** (breaking parameter changes, removed outputs): **New action ID**
  * Example: `monday.get-items` v1.x → `monday.get-items-v2` v2.0.0
  * Old action marked deprecated but remains available
  * Workflows must explicitly migrate to new action ID

**Catalog registration:**
* `ActionCatalog.Id` = `{connector}.{action}` for v1, `{connector}.{action}-v{major}` for v2+
* `ActionCatalog.Version` = full semantic version string
* `[WorkflowAction("monday.get-items", Version = "1.2.0")]` attribute
* Multiple versions can coexist (v1 and v2 side-by-side)

**Deprecation workflow:**
1. Mark old action `IsEnabled=false` in catalog
2. Set `DeprecationNotice` with migration instructions
3. Workflows using deprecated actions show warnings
4. After grace period (e.g., 6 months), remove implementation

### 7.7 Built-in Actions

* `core.echo`: Test action that echoes parameters
* `core.template.render`: Renders document templates
* `core.delay`: Pauses execution for specified duration
* Additional connectors: Monday, Slack, Confluence, Email (implemented separately)

---

## 8. Workflow Context Model

### 8.1 Thread-Safe Store

```csharp
public sealed class WorkflowContext {
  public WorkflowMetadata Metadata { get; }
  public object Trigger { get; }  // immutable trigger snapshot
  private readonly ConcurrentDictionary<string, object?> _data = new();

  public void SetActionOutput(string nodeId, object? output) => _data[nodeId] = output;

  public bool TryGetActionOutput<T>(string nodeId, out T? value) {
    if (_data.TryGetValue(nodeId, out var v) && v is not null) {
      value = (T?)v;
      return true;
    }
    value = default;
    return false;
  }

  public IReadOnlyDictionary<string, object?> Snapshot() =>
    new ReadOnlyDictionary<string, object?>(_data);
}
```

### 8.2 Read-Only Façade

```csharp
public sealed class WorkflowReadonlyContext {
  public object Trigger { get; init; } = default!;
  public IReadOnlyDictionary<string, object?> Data { get; init; } = default!;  // used by Jint/Scriban
}
```

### 8.3 Serialization

* `System.Text.Json` with options:
  * CamelCase property names
  * Ignore nulls on write
  * Max depth increased (for nested objects)
  * Reference handling `IgnoreCycles`

### 8.4 Context Snapshot Pruning

```csharp
public sealed class ContextSnapshotOptions {
  public SnapshotMode Mode { get; init; } = SnapshotMode.SummaryOnly;
  public HashSet<string>? KeysToInclude { get; init; }
  public int MaxContextSizeBytes { get; init; } = 10 * 1024 * 1024; // 10MB
  public OverflowBehavior OverflowBehavior { get; init; } = OverflowBehavior.Fail;
}

public enum SnapshotMode {
  Full,          // Store all outputs
  SummaryOnly,   // Store metadata only (node IDs, sizes, types)
  KeysOnly       // Store only specified keys
}

public enum OverflowBehavior {
  Fail,              // Throw exception if over limit
  AutoPruneOldest,   // Remove oldest outputs
  DropOversize       // Omit large outputs (store metadata)
}
```

* Pruning configured **per-workflow** (definition metadata) or **globally** via options
* Workflow-level settings override global
* Raw action outputs always in `ActionExecutions.OutputsJson`
* Context snapshot for templating/conditions only

---

## 9. Orchestrator Execution Model

### 9.1 Planning & Topology

#### 9.1.1 Publish-Time Validation (Early Failure Detection)

When publishing a workflow (`POST /workflows/{id}/publish`), perform comprehensive validation **before** creating immutable `WorkflowDefinitions` entry:

1. **JSON Schema validation**: Strict adherence to workflow definition schema (§4.1)
2. **Cross-reference checks** (§4.2):
   * `startNode` exists in `nodes`
   * All `edges[].targetNode` reference existing nodes
   * `onFailure` (if present) references existing node
   * For `nodeType=action`: `actionType` is required
   * For `nodeType=subworkflow`: `workflowId` is required
3. **Action availability**: Query `ActionCatalog` to ensure all referenced actions exist and `IsEnabled=true`
   * If action not found or disabled → return **400** with list of missing actions
4. **Jint condition syntax**: Pre-compile all edge conditions to verify JavaScript syntax
   * Catch syntax errors early (before runtime)
   * Store compiled conditions in memory cache
5. **Optional dry-run parameter rendering**: Render all action parameters with mock trigger to detect template errors
   * Enabled via `PublishOptions.ValidateParameterTemplates` (default: false, expensive)
   * Uses empty `trigger={}` and `context={}`

**Benefits:**
* Catch 90% of workflow errors at publish time
* Immutable versions guaranteed valid
* Faster runtime execution (no re-validation)

#### 9.1.2 Runtime Planning

At execution time, load cached or recompute plan:

1. Load `(WorkflowId, Version)` definition from `WorkflowDefinitions`
2. Validate against JSON Schema (defense-in-depth, lightweight)
3. Build **superset graph** including **all** edges (ignore `when`/`condition`)
4. Topologically sort superset to verify **acyclic**
5. Precompute `expectedIncoming[target]` (count of incoming edges from reachable parents)
6. Load pre-compiled Jint conditions & Scriban templates from cache
7. Store plan by `(workflowId, version)` in memory (optionally persist to `WorkflowPlans`)

### 9.2 Run Loop (Bounded Concurrency)

* Process-wide semaphore `MaxParallelActions`
* Workflow-level CancellationTokenSource with `DefaultWorkflowTimeout`
* Seed run-queue with `startNode`
* For each runnable node:
  1. **Render parameters** via Scriban → JSON → `TParams` → validate
  2. **Execute action/subworkflow** under per-node timeout (CTS linked to workflow CTS)
  3. **Apply retry policy** (Polly) on `RetriableFailure` or transient exceptions
  4. **Persist** `ActionExecutions` attempt with parameters/outputs/error
  5. **Update context**: `WorkflowContext.SetActionOutput(nodeId, outputs)`
  6. **Evaluate outgoing edges**: mark satisfied children
  7. **Check join readiness**: when `inDegreeSatisfied[target] == expectedIncoming[target]` → enqueue child

### 9.3 Retry Semantics

* Retries re-enter step 1 (parameter rendering)
* If `rerenderOnRetry=false` (default): reuse persisted `ParametersJson` from first attempt
* If `rerenderOnRetry=true`: re-render parameters each attempt (allows time-based templates)
* Fresh CTS per attempt
* Retry delays **do not** occupy global semaphore
* Attempt metadata persisted: `Attempt`, `RetryCount`, `ErrorJson`
* If runner crashes mid-retry, next pickup resumes from persisted attempt counts

### 9.4 Fail-Fast & Cancellation

* **Unhandled failure** (after all retries): cancel workflow CTS
* **In-flight actions**: may complete if they beat timeout; status recorded (commonly `Succeeded`); workflow still fails
* **Not yet started** nodes: marked `Skipped`
* **Join nodes** waiting on cancelled branches: become **unreachable**, not scheduled
* **Node-level `onFailure`**: synthesize implicit failure edge to `onFailure` node during planning (unless explicit failure edge exists)

### 9.5 Subworkflow Execution

* When `nodeType=subworkflow`:
  1. Render `parameters` → JSON (becomes trigger payload for child)
  2. Call `sp_StartWorkflowExecution` with `p_parentExecutionId`
  3. Insert into `WorkflowExecutionHierarchy`
  4. If `waitForCompletion=true`: poll/wait for child execution completion
  5. If `waitForCompletion=false`: fire-and-forget, node immediately succeeds
  6. Merge child outputs: `context.data[nodeId].outputs = childExecutionOutputs`
* **Guardrails**:
  * `MaxWorkflowNestingDepth` (default 5)
  * `AllowRecursion` (default false): detect cycles in hierarchy
  * Child inherits `TenantId` and `CorrelationId` from parent

---

## 10. Options

### 10.1 Orchestration

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
```

### 10.2 Error Handling

```csharp
public sealed class ErrorHandlingOptions {
  public bool UseDeadLetterQueue { get; init; } = true;
  public string DeadLetterQueueName { get; init; } = "workflow-dlq";
  public int MaxPoisonMessageRetries { get; init; } = 3;
  public TimeSpan PoisonMessageDelay { get; init; } = TimeSpan.FromMinutes(5);
}
```

### 10.3 Scalability

```csharp
public sealed class ScalabilityOptions {
  public int MaxNodesPerWorkflow { get; init; } = 1000;
  public int MaxContextSizeBytes { get; init; } = 10 * 1024 * 1024;
  public bool EnableDistributedLocking { get; init; } = false;  // Redis-based
  public string? RedisConnectionString { get; init; }
}
```

### 10.4 Catalog

```csharp
public sealed class WorkflowCatalogOptions {
  public bool AutoRegisterActionsOnStartup { get; init; } = true;
  public bool ValidateActionSchemasOnStartup { get; init; } = true;
  public bool AllowDraftExecution { get; init; } = false;
}
```

### 10.5 Subworkflows

```csharp
public sealed class SubworkflowOptions {
  public int MaxNestingDepth { get; init; } = 5;
  public bool AllowRecursion { get; init; } = false;
  public TimeSpan DefaultChildTimeout { get; init; } = TimeSpan.FromHours(1);
}
```

### 10.6 Triggers

```csharp
public sealed class TriggerOptions {
  public bool EnableWebhooks { get; init; } = true;
  public bool EnableSchedules { get; init; } = true;
  public bool EnableEvents { get; init; } = true;
  public string WebhookBasePath { get; init; } = "/api/v1/triggers";
  public string SchedulerProvider { get; init; } = "Quartz";  // or "Hangfire"
}
```

---

## 11. Queueing Model & DLQ

### 11.1 Primary Queue

* **Options**: Azure Service Bus, RabbitMQ, AWS SQS
* **MVP**: DB Outbox table + `BackgroundService` polling with `SELECT ... FOR UPDATE SKIP LOCKED`
* **Message format**: `{ executionId, workflowId, version, requestId, triggerPayload }`

### 11.2 Dead Letter Queue

* If deserialization/validation fails → move to `DeadLetters` table
* If retries of dequeue fail → DLQ with `Reason` and `PayloadJson`
* Admin endpoints to requeue or purge

### 11.3 Concurrency Control

* One active runner per `WorkflowExecutionId` enforced by `sp_TryAcquireExecution`
* DB compare-and-set: `Status` transition `Pending→Running`
* Optional distributed locks (Redis) for message dequeue deduplication only

---

## 12. Authentication, Authorization, Rate Limiting

### 12.1 JWT Authentication

* Required scopes:
  * `workflows:execute` for `POST /workflows/{id}/execute`
  * `workflows:read` for reads
  * `workflows:manage` for create/update/delete
  * `templates:read` for template access
  * `templates:manage` for template authoring

### 12.2 Multi-Tenancy

* Require `tenant_id` claim in JWT
* Row-level filters by `TenantId` column
* Workflow isolation enforced at API layer

### 12.3 Rate Limiting

* **Per-token**: 60 req/min, burst 120
* **Per-workflow**: N executions/min (configurable)
* Headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `Retry-After`

### 12.4 MVP Approach

* Auth stubbed (no enforcement initially)
* JWT validation enabled but not required
* Rate limiting implemented but lenient defaults

### 12.5 Principal & Impersonation (Loose MVP → Strict OBO)

**Design goal**: Start with minimal auth, upgrade to on-behalf-of (OBO) later without breaking changes.

#### 12.5.1 Principal Object

Workflows track **who initiated** execution, even if the engine runs with elevated credentials.

**Execute request includes optional `principal`:**
```json
{
  "requestId": "abc-123",
  "principal": {
    "userId": "U123",
    "displayName": "Doug",
    "email": "doug@example.com"
  },
  "trigger": { ... }
}
```

* If `principal` omitted → defaults to `{ userId: "system", displayName: "System", email: null }`
* Stored in `WorkflowExecutions`: `RequesterUserId`, `RequesterEmail`, `RequesterDisplayName`
* Used for audit logs, metrics, and downstream headers

#### 12.5.2 Connector Credentials (MVP: Static Tokens)

Actions need credentials to call external systems (Monday, Slack, etc.).

**MVP approach**: Single static token per connector (no per-user tokens yet).

**Interface:**
```csharp
public interface IConnectorCredentialProvider {
  string? GetToken(string connectorId);
}
```

**Environment variable implementation:**
```csharp
public sealed class EnvVarCredentialProvider : IConnectorCredentialProvider {
  public string? GetToken(string connectorId) =>
    Environment.GetEnvironmentVariable($"CONNECTOR_{connectorId.ToUpperInvariant()}_TOKEN");
}
```

**Example environment variables:**
* `CONNECTOR_SLACK_TOKEN` → Slack bot token
* `CONNECTOR_MONDAY_TOKEN` → Monday API key
* `CONNECTOR_CONFLUENCE_TOKEN` → Confluence personal access token

Actions retrieve via DI:
```csharp
var provider = context.Services.GetRequiredService<IConnectorCredentialProvider>();
var token = provider.GetToken("monday");
```

#### 12.5.3 Pass-Through Headers (Future OBO Ready)

Actions send these headers to downstream systems, enabling future OBO without code changes:

* `Authorization: Bearer <connector-token>` (from credential provider)
* `X-Acting-User-Id: <principal.userId>`
* `X-Acting-User-Email: <principal.email>`
* `X-Acting-User-Name: <principal.displayName>`
* `X-Correlation-Id: <execution.correlationId>`

**Future upgrade path:**
1. Replace `EnvVarCredentialProvider` with `VaultCredentialProvider` (per-user tokens)
2. Downstream systems honor `X-Acting-User-*` headers for RBAC
3. No action code changes required

#### 12.5.4 Auth Options (Loose → Strict Toggle)

```csharp
public sealed class AuthOptions {
  public bool AllowLooseAuth { get; init; } = true;         // MVP: true
  public bool RequirePrincipalForExecute { get; init; } = false;
  public bool ValidateDownstreamPermissions { get; init; } = false;
}
```

**MVP configuration**: `AllowLooseAuth=true`
* No JWT required
* Principal optional
* Static connector tokens

**Production configuration**: `AllowLooseAuth=false`
* JWT required with valid claims
* Principal required and validated
* Per-user tokens from Vault
* Downstream systems enforce RBAC via pass-through headers

---

## 13. API Specification (v1)

### 13.1 Workflow Execution

**POST /api/v1/workflows/{workflowId}/execute**

Request:
```json
{
  "requestId": "ext-event-12345",
  "principal": {
    "userId": "U123",
    "displayName": "Doug",
    "email": "doug@example.com"
  },
  "trigger": { "source": "channel-x", "userId": "abc" },
  "priority": 5,
  "tenantId": "acme",
  "spec": { /* optional structured payload */ }
}
```

Response `202 Accepted` (new) or `200 OK` (existing):
```json
{
  "executionId": "...",
  "status": "Pending",
  "statusUrl": "/api/v1/executions/..."
}
```

* **Idempotency**: If `(WorkflowId, requestId)` exists → 200 with existing execution
* If `requestId` exists for different workflow → 409 Conflict

### 13.2 Execution Queries

**GET /api/v1/executions/{executionId}?include=actions,resources,events&limit=100&offset=0**

* Supports pagination for `events` and `actions`
* `include=children` expands subworkflow executions

**GET /api/v1/executions/{executionId}/hierarchy**

* Returns full parent-child execution tree

### 13.3 Workflow Management

**GET /api/v1/workflows**

* List all workflows
* Query params: `?tenantId=&status=&isEnabled=&search=`
* Status filter: `Draft`, `Active`, `Archived`

**GET /api/v1/workflows/{id}?version={n}**

* Fetch specific version (omit for CurrentVersion)
* Returns workflow definition + metadata

**POST /api/v1/workflows**

* Create new Draft workflow OR update existing Draft
* Validates JSON Schema + cross-references at POST time
* Returns 400 with `WFENG005` if validation fails
* **Rules**:
  * If workflow doesn't exist → create new with `Status=Draft`
  * If workflow exists and `Status=Draft` → update `DefinitionJson` (mutable)
  * If workflow exists and `Status=Active|Archived` → **400 error** (immutable)
* Does NOT create `WorkflowDefinitions` entry (happens on publish)

**POST /api/v1/workflows/{id}/publish?autoActivate=true**

* Transition: `Draft → Active` (if `autoActivate=true`)
* Query parameter `autoActivate` (default: `true`):
  * `true`: Creates version AND sets `Status=Active`, `IsEnabled=true`
  * `false`: Creates version but leaves `Status=Draft` (CI/CD staging)
* Validates workflow definition (JSON Schema + cross-references + action availability)
* Computes checksum of `DefinitionJson`
* If checksum differs from latest version → auto-increments version, creates `WorkflowDefinitions` entry
* If checksum matches → no-op, returns existing version (idempotent)
* If `autoActivate=true`: Sets `Status=Active`, `IsEnabled=true`, updates `Workflows.CurrentVersion`
* Returns: `{ workflowId, version, status: "Active"|"Draft" }`

**POST /api/v1/workflows/{id}/archive**

* Transition: `Active → Archived`
* Sets `Status=Archived`, `IsEnabled=false`
* In-flight executions continue
* Pending executions rejected by runner
* All triggers for this workflow disabled (not deleted)
* Returns: `{ workflowId, status: "Archived" }`

**POST /api/v1/workflows/{id}/reactivate** (optional)

* Transition: `Archived → Active`
* Sets `Status=Active`, `IsEnabled=true`
* Re-enables triggers

**DELETE /api/v1/workflows/{id}**

* Hard delete (removes from Workflows table)
* **Only allowed for Draft workflows**
* Active/Archived workflows must be archived first
* Cascades to WorkflowDefinitions, triggers (ON DELETE CASCADE)

**POST /api/v1/executions/{id}/cancel**

* Best-effort cancellation via CTS
* Marks `Cancelled` when runner acknowledges

### 13.4 Actions & Connectors

**GET /api/v1/connectors**

* List all registered connectors

**GET /api/v1/connectors/{id}**

* Get connector details

**GET /api/v1/connectors/{id}/actions**

* List actions for connector

**GET /api/v1/actions**

* List all actions
* Query params: `?connectorId=&category=&search=`

**GET /api/v1/actions/{id}**

* Get action metadata (schemas, description)

### 13.5 Workflow Templates

**GET /api/v1/templates/workflows**

* List workflow templates
* Query params: `?category=&isOfficial=&search=`

**GET /api/v1/templates/workflows/{id}**

* Get template details

**POST /api/v1/templates/workflows/{id}/instantiate**

Request:
```json
{
  "displayName": "My Custom Workflow",
  "config": { /* values per template ConfigSchema */ }
}
```

Response:
```json
{
  "workflowId": "...",
  "status": "Draft"
}
```

### 13.6 Document Templates

**GET /api/v1/templates?team={team}&set={templateSetId}**

* List document templates

**GET /api/v1/templates/{id}?version={n}**

* Get template definition (fields, body, metadata)

**POST /api/v1/templates/{id}/render?version={n}**

Request:
```json
{
  "answers": { /* key→value per FieldsJson */ },
  "extras": { /* optional */ }
}
```

Response:
```json
{
  "renderedBody": "..."
}
```

### 13.7 Triggers

**GET /api/v1/triggers**

* List triggers
* Query params: `?workflowId=&triggerType=&isEnabled=`

**GET /api/v1/triggers/{id}**

* Get trigger details

**POST /api/v1/triggers**

* Create trigger
* Auto-registers webhook endpoint if `triggerType=Webhook`

**PUT /api/v1/triggers/{id}**

* Update trigger config

**DELETE /api/v1/triggers/{id}**

* Delete trigger

**POST /api/v1/triggers/{id}/fire**

* Manual trigger fire (for testing)

**POST /api/v1/triggers/{triggerId}** (dynamic webhook endpoints)

* Auto-created per webhook trigger
* Receives arbitrary JSON payload
* Validates `ActivationCondition` if present
* Enqueues workflow execution

---

## 14. Observability & Ops

### 14.1 Logging

* **Serilog** with structured JSON output
* Standard properties:
  * `CorrelationId`
  * `WorkflowId`
  * `WorkflowVersion`
  * `ExecutionId`
  * `NodeId`
  * `ActionType`
  * `PrincipalUserId` (from `WorkflowExecutions.RequesterUserId`)
  * `PrincipalEmail` (from `WorkflowExecutions.RequesterEmail`)
  * `TenantId`

### 14.2 Tracing

* **OpenTelemetry** `ActivitySource`
* Spans per:
  * Workflow execution (parent span)
  * Node execution (child span)
  * Action execution (child span)
  * Subworkflow invocation (linked span)
* Tags: retry attempt, status, duration

### 14.3 Metrics (Prometheus/OpenTelemetry)

* `workflow_duration_seconds{workflowId,status}` (histogram)
* `workflow_success_total{workflowId}` (counter)
* `workflow_failure_total{workflowId,reason}` (counter)
  * reason ∈ `{action_failed, timeout, cancelled, validation_error}`
* `action_duration_seconds{actionType}` (histogram)
* `action_retry_total{actionType}` (counter)
* `action_failure_total{actionType,status}` (counter)
* `queue_depth{workflowId}` (gauge)
* `template_render_duration_seconds` (histogram)
* `condition_eval_duration_seconds` (histogram)
* `subworkflow_invocation_total{parentWorkflowId}` (counter)

### 14.4 Health Checks

* `/health/live`: Basic liveness probe
* `/health/ready`: Readiness probe with:
  * Database connectivity
  * Queue connectivity
  * Action registry status

---

## 15. Examples

### 15.1 Simple Linear Workflow

```json
{
  "id": "get-monday-status",
  "displayName": "Get Monday Status Report",
  "startNode": "fetch-items",
  "nodes": [
    {
      "id": "fetch-items",
      "actionType": "monday.get-items",
      "parameters": {
        "boardId": "{{ trigger.boardId }}",
        "filter": {
          "rules": [{
            "column": "Status",
            "operator": "eq",
            "value": "In Progress"
          }]
        }
      },
      "policies": {
        "retry": {
          "maxAttempts": 3,
          "baseDelayMs": 300,
          "backoffFactor": 2.0,
          "jitter": true
        }
      },
      "edges": [
        { "targetNode": "post-slack", "when": "success" }
      ]
    },
    {
      "id": "post-slack",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Found {{ context.data['fetch-items'].items.length }} items in progress."
      }
    }
  ]
}
```

### 15.2 Branching with Conditions

```json
{
  "id": "onboard-project",
  "displayName": "Onboard New Project",
  "startNode": "get-item",
  "nodes": [
    {
      "id": "get-item",
      "actionType": "monday.get-items",
      "parameters": {
        "boardId": "{{ trigger.boardId }}",
        "filter": {
          "rules": [{
            "column": "Item ID",
            "operator": "eq",
            "value": "{{ trigger.itemId }}"
          }]
        }
      },
      "edges": [
        {
          "targetNode": "create-confluence",
          "when": "success",
          "condition": "context.data['get-item'].items[0].Status === 'Approved'"
        },
        {
          "targetNode": "notify-not-approved",
          "when": "success",
          "condition": "context.data['get-item'].items[0].Status !== 'Approved'"
        }
      ],
      "onFailure": "notify-error"
    },
    {
      "id": "create-confluence",
      "actionType": "confluence.create-page",
      "parameters": {
        "space": "PROJECTS",
        "title": "{{ context.data['get-item'].items[0].Name }} - Project Brief"
      }
    },
    {
      "id": "notify-not-approved",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Project '{{ context.data['get-item'].items[0].Name }}' is not approved."
      }
    },
    {
      "id": "notify-error",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Onboarding failed at {{ context.data['get-item'].errorNode ?? 'unknown' }}."
      }
    }
  ]
}
```

### 15.3 Fan-out/Fan-in

```json
{
  "id": "fanout-fanin",
  "displayName": "Fan-out/Fan-in Example",
  "startNode": "A",
  "nodes": [
    {
      "id": "A",
      "actionType": "core.echo",
      "parameters": { "msg": "start" },
      "edges": [
        { "targetNode": "B", "when": "success", "condition": "true" },
        { "targetNode": "C", "when": "success", "condition": "false" }
      ]
    },
    {
      "id": "B",
      "actionType": "core.echo",
      "parameters": { "msg": "B" },
      "edges": [ { "targetNode": "D", "when": "success" } ]
    },
    {
      "id": "C",
      "actionType": "core.echo",
      "parameters": { "msg": "C" },
      "edges": [ { "targetNode": "D", "when": "success" } ]
    },
    {
      "id": "D",
      "actionType": "core.echo",
      "parameters": { "msg": "Join" }
    }
  ]
}
```

Note: C is skipped (condition false), so D executes when only B completes.

### 15.4 Subworkflow Example

```json
{
  "id": "parent-workflow",
  "displayName": "Parent with Child Workflow",
  "startNode": "invoke-child",
  "nodes": [
    {
      "id": "invoke-child",
      "nodeType": "subworkflow",
      "workflowId": "child-workflow",
      "workflowVersion": 2,
      "parameters": {
        "input": "{{ trigger.data }}"
      },
      "waitForCompletion": true,
      "edges": [
        { "targetNode": "process-result", "when": "success" }
      ]
    },
    {
      "id": "process-result",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "Child workflow completed with: {{ context.data['invoke-child'].outputs.result }}"
      }
    }
  ]
}
```

### 15.5 Template-Driven Document Creation

```json
{
  "id": "create-project-brief",
  "displayName": "Create Project Brief from Template",
  "startNode": "render-template",
  "nodes": [
    {
      "id": "render-template",
      "actionType": "core.template.render",
      "parameters": {
        "templateSetId": "project-briefs",
        "templateId": "mobile-app-brief",
        "version": 4,
        "answers": "{{ trigger.spec.answers }}"
      },
      "edges": [
        { "targetNode": "create-confluence", "when": "success" }
      ]
    },
    {
      "id": "create-confluence",
      "actionType": "confluence.create-page",
      "parameters": {
        "space": "PROJECTS",
        "title": "{{ trigger.spec.answers.title }} - Project Brief",
        "body": "{{ context.data['render-template'].body }}"
      }
    }
  ]
}
```

### 15.6 Retry with Deterministic Parameters

```json
{
  "id": "retry-demo",
  "displayName": "Retry with Deterministic Params",
  "startNode": "do-work",
  "nodes": [
    {
      "id": "do-work",
      "actionType": "core.sometimes-fails",
      "parameters": {
        "payload": "{{ trigger.payload }}",
        "timestamp": "{{ trigger.timestamp }}"
      },
      "policies": {
        "retry": { "maxAttempts": 3, "baseDelayMs": 200 },
        "rerenderOnRetry": false
      }
    }
  ]
}
```

### 15.7 End-to-End: Slack Intake → Document Render → Confluence

This example demonstrates the full lifecycle of document template usage, from user intake to published output.

**Scenario**: User initiates a project brief from Slack. The workflow collects answers, renders a document template, and publishes to Confluence.

**Step 1: Slack slash command triggers workflow**

User types: `/create-project-brief`

Slack bot presents a form based on `TemplateDefinitions.FieldsJson` (fetched from `GET /templates/project-brief/mobile-app?version=1`).

User fills out:
- Title: "Recommendation Engine Redesign"
- Goal: "Improve personalization accuracy by 25%"
- Stakeholders: ["@doug", "@alice"]
- Deadline: "2025-12-01"
- Platform: "Web + Mobile"

Slack bot calls workflow execution:

```http
POST /api/v1/workflows/create-project-brief/execute
{
  "requestId": "slack-cmd-789",
  "principal": {
    "userId": "U123",
    "displayName": "Doug",
    "email": "doug@example.com"
  },
  "trigger": {
    "source": "slack",
    "channelId": "C456",
    "userId": "U123"
  },
  "spec": {
    "templateSetId": "project-briefs",
    "templateId": "mobile-app",
    "templateVersion": 1,
    "answers": {
      "title": "Recommendation Engine Redesign",
      "goal": "Improve personalization accuracy by 25%",
      "stakeholders": ["@doug", "@alice"],
      "deadline": "2025-12-01",
      "platform": "Web + Mobile"
    }
  }
}
```

**Step 2: Workflow definition**

```json
{
  "id": "create-project-brief",
  "displayName": "Create Project Brief from Template",
  "startNode": "render-template",
  "nodes": [
    {
      "id": "render-template",
      "actionType": "core.template.render",
      "parameters": {
        "templateSetId": "{{ spec.templateSetId }}",
        "templateId": "{{ spec.templateId }}",
        "version": "{{ spec.templateVersion }}",
        "answers": "{{ spec.answers }}"
      },
      "edges": [
        { "targetNode": "create-confluence", "when": "success" }
      ]
    },
    {
      "id": "create-confluence",
      "actionType": "confluence.create-page",
      "parameters": {
        "space": "PROJECTS",
        "title": "{{ spec.answers.title }} - Project Brief",
        "body": "{{ context.data['render-template'].body }}"
      },
      "edges": [
        { "targetNode": "notify-slack", "when": "success" }
      ]
    },
    {
      "id": "notify-slack",
      "actionType": "slack.post-message",
      "parameters": {
        "channelId": "{{ trigger.channelId }}",
        "message": "✅ Project brief created: {{ context.data['create-confluence'].url }}"
      }
    }
  ]
}
```

**Step 3: Action execution flow**

1. **`render-template` node**:
   - Loads `TemplateDefinitions` row for `(project-briefs, mobile-app, version=1)`
   - Applies Scriban rendering to `BodyTemplate` with `answers` from trigger
   - Outputs: `{ body: "<h1>Recommendation Engine Redesign</h1>...", metadata: {...} }`

2. **`create-confluence` node**:
   - Retrieves connector token: `IConnectorCredentialProvider.GetToken("confluence")`
   - Calls Confluence API with headers:
     - `Authorization: Bearer <confluence-token>`
     - `X-Acting-User-Id: U123`
     - `X-Acting-User-Email: doug@example.com`
     - `X-Correlation-Id: <executionId>`
   - Creates page in PROJECTS space
   - Outputs: `{ pageId: "123456", url: "https://wiki.example.com/...}" }`

3. **`notify-slack` node**:
   - Posts success message to Slack channel
   - Includes Confluence page URL

**Result**: Doug receives Slack notification with link to newly created Confluence page containing the rendered project brief.

---

## 16. Testing Strategy

### 16.1 Unit Tests

* **Graph validator**: cycles, unreachable nodes, invalid refs
* **Edge logic**: `when`/`condition` permutations, join readiness, routePolicy
* **Template failures**: strict nulls/undefined → error paths & retries
* **Jint timeouts/memory caps**: edge evaluates false, events recorded
* **Parameter binding**: Scriban render → JSON → TParams → validation
* **Retry logic**: attempt/retryCount semantics, rerenderOnRetry

### 16.2 Integration Tests

* **Repositories**: Postgres via Testcontainers
* **Stored procedures**: idempotency, error codes, state transitions
* **Runner**: MockHttp for connectors; verify retries, backoff, fail-fast
* **Resource links**: unique wins, duplicate reads existing, conflict throws
* **Subworkflows**: hierarchy tracking, output merging, depth limits

### 16.3 End-to-End Tests

* **Full DAG**: parallel branches, transient failures → retries, permanent failures → cancel + join skip
* **Subworkflow invocation**: parent waits for child, outputs merged
* **Trigger activation**: webhook → execution, schedule → periodic execution
* **Template rendering**: intake → render → create document

### 16.4 Performance Tests

* **Large DAGs**: N=1k nodes, fan-out/fan-in
* **High concurrency**: multiple runners, queue throughput
* **Context size**: overflow behavior, pruning
* **Tune**: `MaxParallelActions`, retry delays, timeout values

### 16.5 Security Tests

* **Jint sandbox escapes**: attempt CLR access, infinite loops, memory bombs
* **Scriban template injection**: file includes, script execution
* **JSON bombs**: deeply nested structures, large payloads
* **Stored procedures**: SQL injection via parameters
* **JWT/claims enforcement**: tenant isolation, scope validation

---

## 17. Open Questions & Defaults

1. **In-flight version upgrades**: Default = in-flight executions continue on their original `(WorkflowId, Version)`
2. **Trigger shape**: Default free-form; per-workflow optional JSON schema via `triggerSchema`
3. **Who writes `WorkflowResourceLinks`?**: Actions return `ResourceLinks`; orchestrator persists after `Succeeded` result
4. **Idempotency checks in actions**: First consult `WorkflowResourceLinks`, then optional connector-side lookup
5. **Subworkflow recursion**: Default disallowed; depth limit enforced
6. **Draft execution**: Default disallowed; controlled by `WorkflowCatalogOptions.AllowDraftExecution`

---

## 18. Implementation Order

### Phase 1: Foundation
1. Database migrations (all tables, indexes, stored procedures)
2. FluentMigrator setup with schema_version tracking
3. Dapper repository base classes

### Phase 2: Core Engine
4. JSON Schema validation (workflow definitions)
5. Graph validator (cross-references, cycles, reachability)
6. Scriban template engine with sandboxing
7. Jint condition evaluator with sandboxing
8. WorkflowContext (thread-safe store, snapshot, pruning)

### Phase 3: Action System
9. IWorkflowAction interface & base classes
10. Action registry (reflection-based discovery)
11. Parameter binding pipeline (Scriban → JSON → TParams → validation)
12. Built-in actions (core.echo, core.delay)
13. ActionCatalog integration (upsert on startup)

### Phase 4: Orchestration
14. Conductor planning (superset graph, topology, pre-compilation)
15. Conductor execution loop (bounded concurrency, retry, fail-fast)
16. Edge evaluation & routing (parallel/firstMatch)
17. Join node semantics
18. Resource link persistence & idempotency

### Phase 5: Subworkflows
19. Subworkflow node execution
20. WorkflowExecutionHierarchy tracking
21. Child output merging
22. Depth & recursion limits

### Phase 6: Queue & Runner
23. DB outbox table + polling BackgroundService
24. sp_TryAcquireExecution concurrency control
25. Dead letter queue
26. Poison message handling

### Phase 7: API Layer
27. Workflow management endpoints (CRUD, publish, archive)
28. Execution endpoints (execute, query, cancel, hierarchy)
29. ActionCatalog & Connector endpoints
30. Workflow template endpoints
31. Document template endpoints

### Phase 8: Triggers
32. WorkflowTriggers CRUD
33. Webhook trigger (dynamic endpoint registration)
34. Schedule trigger (Quartz.NET integration)
35. Event trigger (internal bus integration)
36. ActivationCondition evaluation

### Phase 9: Observability
37. Serilog structured logging
38. OpenTelemetry tracing
39. Prometheus metrics
40. Health checks

### Phase 10: Auth & Rate Limiting
41. JWT validation middleware
42. Scope-based authorization
43. Multi-tenancy filtering
44. Rate limiting (per-token, per-workflow)

### Phase 11: Testing
45. Unit tests (all components)
46. Integration tests (DB, runner, end-to-end)
47. Performance tests (large DAGs, high concurrency)
48. Security tests (sandbox escapes, injection)

### Phase 12: Documentation & Deployment
49. API documentation (OpenAPI/Swagger)
50. Deployment guide (Docker, k8s, config)
51. Migration guide (schema versioning, rollback)
52. Connector development guide

---

## 19. Migration & Rollback Strategy

### 19.1 Migration Tooling

* **FluentMigrator** or **EF Core Migrations**
* `schema_version` table tracks applied migrations
* Each migration tagged with timestamp + description

### 19.2 Migration Practices

* Favor additive changes (new columns, tables)
* Ensure rolling compatibility (old runners work with new schema)
* Use feature flags for breaking changes
* Shadow writes for risky schema changes

### 19.3 Deployment Strategy

* **Blue/green deployment**: new version alongside old
* Health checks ensure new version stable before cutover
* Gradual traffic shift (canary deployment)

### 19.4 Rollback

* Deploy previous image
* Run safe down-migrations (no destructive drops without backup)
* Test rollback scenarios in staging

---

## 20. Error Codes Reference

* **WFENG001** `REQUEST_ID_CONFLICT_OTHER_WORKFLOW` — requestId exists for different workflow
* **WFENG002** `ILLEGAL_STATE_TRANSITION` — invalid execution status transition
* **WFENG003** `RESOURCE_LINK_CONFLICT_OTHER_EXECUTION` — external resource already linked elsewhere
* **WFENG004** `TEMPLATE_NOT_FOUND` — document template not found
* **WFENG005** `VALIDATION_ERROR` — JSON schema validation failed

---

**This specification consolidates Rev 5 and Rev 6, providing a complete, implementation-ready design for the DataWorkflows engine.**
