# DataWorkflows Engine — Implementation Plan

> **Methodology**: Vertical Slice / E2E-First
>
> **Goal**: Build working features incrementally. Each phase delivers a complete, testable capability.
>
> **Spec Reference**: `workflow_engine_spec.md` (authoritative source)

---

## LLM Consumption Guide

**This plan is optimized for feeding to LLMs with limited context windows:**

1. **Section Anchors**: References use `§X.Y Section-Name | Lines N-M (approx)` format
   - Lines drift with edits; section names and anchors are stable
   - When lines shift, search spec for section heading instead

2. **Task Headers**: Every step has 3 fields:
   - **Inputs**: Spec sections, existing files, config to read before starting
   - **Edits**: Exact file paths with (create/modify) annotation
   - **Checks**: Build status, curl commands, DB queries to verify success

3. **Named Artifacts**: File paths, class names, method signatures are explicit (not "add a controller")

4. **Fixtures & Commands**: Canonical test payloads in `fixtures/` directory with curl/HTTPie commands

5. **Auth Toggles**: Phases 1-6 use `AllowLooseAuth=true` (no JWT). Phase 10 Step 157 flips to strict mode.

---

## Current State Assessment

**Already Complete:**
- Solution structure exists (`DataWorkflows.sln`)
- DataWorkflows.Engine project exists as ASP.NET Core 8.0 Web API
- Basic dependencies: Polly, Dapper, Npgsql
- DI container configured
- Swagger configured
- `IWorkflowOrchestrator` interface defined
- `WorkflowOrchestrator` stub class exists
- `WorkflowController` with POST /{id}/execute endpoint
- HealthController exists
- 4+ connector projects exist (Monday, Slack, Confluence, Outlook)
- Test projects exist for Monday connector

**Needs Implementation:**
- Database schema (tables, migrations, stored procedures)
- Workflow definition models & parsing
- Action system (IWorkflowAction interface, registry)
- Actual orchestration logic (conductor, execution loop)
- Templating engines (Scriban for parameters, Jint for conditions)
- Workflow lifecycle (Draft/Publish/Archive)
- All the rest!

**Starting Point:** We're picking up at Step 2 (add missing dependencies), then Step 6 (create database tables).

---

## Philosophy

This plan follows a **vertical slice approach** rather than horizontal layering:

- **Phase 1 delivers a working E2E workflow** (even if minimal)
- **Each subsequent phase adds ONE complete feature**
- **Solution compiles and tests pass after every step**
- **No broken builds between steps**

**Why this approach?**
- Get feedback fast (working demo in Phase 1)
- De-risk integration issues early
- Each phase independently valuable
- Easy to pause/resume at phase boundaries

---

## Fixtures Directory Structure

Create this structure at repo root for test payloads:

```
H:/Development/DataConnectors/fixtures/
├── phase1/
│   ├── simple-echo-workflow.json
│   └── execute-request.json
├── phase2/
│   ├── retry-workflow.json
│   └── transient-failure-trigger.json
├── phase3/
│   ├── fanout-fanin-workflow.json
│   └── conditional-branch-workflow.json
├── phase4/
│   └── templated-params-workflow.json
├── phase5/
│   ├── monday-get-items-trigger.json
│   └── slack-post-message-trigger.json
└── [additional phases...]
```

**Note**: Fixtures are created in their respective phase steps.

---

## Table of Contents

### **PHASE 1: Minimal E2E Workflow** (Steps 1-30)
**Goal**: Execute a simple 2-node linear workflow via API, store results in DB
**Auth Mode**: `AllowLooseAuth=true` (no JWT required)

#### Scaffolding (Steps 1-5)
- [Step 1](#step-1): Create .NET solution structure
- [Step 2](#step-2): Add core dependencies (Dapper, Npgsql, FluentValidation, etc.)
- [Step 3](#step-3): Configure DI container and appsettings
- [Step 4](#step-4): Add health check endpoint
- [Step 5](#step-5): Verify solution builds and health check responds

#### Minimal Database (Steps 6-10)
- [Step 6](#step-6): Create Workflows table (minimal columns only)
- [Step 7](#step-7): Create WorkflowDefinitions table
- [Step 8](#step-8): Create WorkflowExecutions table (no Principal yet)
- [Step 9](#step-9): Create ActionExecutions table
- [Step 10](#step-10): Add Dapper repositories for above tables

#### Basic Models & Parsing (Steps 11-15)
- [Step 11](#step-11): Define WorkflowDefinition C# model (minimal)
- [Step 12](#step-12): Add JSON deserializer for workflow definitions
- [Step 13](#step-13): Create IWorkflowAction interface
- [Step 14](#step-14): Implement core.echo action (no parameters yet)
- [Step 15](#step-15): Add action registry (hardcoded echo for now)

#### Simple Conductor (Steps 16-20)
- [Step 16](#step-16): Create WorkflowContext (in-memory dictionary)
- [Step 17](#step-17): Create WorkflowConductor with linear execution
- [Step 18](#step-18): Execute nodes sequentially (ignore edges)
- [Step 19](#step-19): Record ActionExecutions to DB
- [Step 20](#step-20): Return execution result

#### API Endpoint (Steps 21-25)
- [Step 21](#step-21): Create ExecuteController with POST /execute
- [Step 22](#step-22): Parse workflow from JSON string (no validation)
- [Step 23](#step-23): Call conductor, return execution ID
- [Step 24](#step-24): Add GET /executions/{id} (basic)
- [Step 25](#step-25): Test with Postman/curl

#### E2E Verification (Steps 26-30)
- [Step 26](#step-26): Create test workflow JSON (2 echo nodes)
- [Step 27](#step-27): Execute via API
- [Step 28](#step-28): Query execution status
- [Step 29](#step-29): Verify ActionExecutions in DB
- [Step 30](#step-30): **MILESTONE: Working E2E!** 🎉

---

### **PHASE 2: Retries & Error Handling** (Steps 31-40)
**Goal**: Add retry policies, fail-fast, and proper error handling
**Auth Mode**: `AllowLooseAuth=true`

- [Step 31](#step-31): Add Polly dependency
- [Step 32](#step-32): Implement RetryPolicyOptions
- [Step 33](#step-33): Add Attempt/RetryCount columns to ActionExecutions
- [Step 34](#step-34): Wrap action execution in Polly retry
- [Step 35](#step-35): Record each attempt in ActionExecutions
- [Step 36](#step-36): Implement ActionExecutionStatus.RetriableFailure
- [Step 37](#step-37): Add fail-fast cancellation token propagation
- [Step 38](#step-38): Update core.echo to support simulated failures
- [Step 39](#step-39): Test retry with transient failure
- [Step 40](#step-40): **MILESTONE: Retry working**

---

### **PHASE 3: Branching & Conditions** (Steps 41-50)
**Goal**: Add edges, Jint conditions, and parallel execution
**Auth Mode**: `AllowLooseAuth=true`

- [Step 41](#step-41): Add Jint dependency
- [Step 42](#step-42): Implement JintConditionEvaluator
- [Step 43](#step-43): Parse edges from workflow definition
- [Step 44](#step-44): Evaluate edge when/condition
- [Step 45](#step-45): Build superset graph
- [Step 46](#step-46): Implement join node logic (expectedIncoming)
- [Step 47](#step-47): Add parallel execution with semaphore
- [Step 48](#step-48): Test fan-out/fan-in workflow
- [Step 49](#step-49): Test conditional branching
- [Step 50](#step-50): **MILESTONE: Branching working**

---

### **PHASE 4: Parameter Templating** (Steps 51-60)
**Goal**: Add Scriban parameter rendering and action parameter binding
**Auth Mode**: `AllowLooseAuth=true`

- [Step 51](#step-51): Add Scriban dependency
- [Step 52](#step-52): Implement TemplateEngine with sandboxing
- [Step 53](#step-53): Add parameters object to Node definition
- [Step 54](#step-54): Render parameters with Scriban before action execution
- [Step 55](#step-55): Add typed parameter binding (TParams)
- [Step 56](#step-56): Update core.echo to accept typed parameters
- [Step 57](#step-57): Store rendered ParametersJson in ActionExecutions
- [Step 58](#step-58): Implement rerenderOnRetry policy
- [Step 59](#step-59): Test templating with trigger data
- [Step 60](#step-60): **MILESTONE: Templating working**

---

### **PHASE 5: Real Connectors** (Steps 61-75)
**Goal**: Add Monday and Slack connectors with real API integration
**Auth Mode**: `AllowLooseAuth=true`, env var credentials

#### Connector Infrastructure (Steps 61-65)
- [Step 61](#step-61): Add Connectors table
- [Step 62](#step-62): Add ActionCatalog table
- [Step 63](#step-63): Implement IConnectorCredentialProvider
- [Step 64](#step-64): Add EnvVarCredentialProvider implementation
- [Step 65](#step-65): Add pass-through headers helper

#### Monday Connector (Steps 66-70)
- [Step 66](#step-66): Create Monday connector project
- [Step 67](#step-67): Implement monday.get-items action
- [Step 68](#step-68): Add Monday filter translation (reference existing code)
- [Step 69](#step-69): Register in ActionCatalog
- [Step 70](#step-70): Test Monday workflow E2E

#### Slack Connector (Steps 71-75)
- [Step 71](#step-71): Create Slack connector project
- [Step 72](#step-72): Implement slack.post-message action
- [Step 73](#step-73): Register in ActionCatalog
- [Step 74](#step-74): Test Slack workflow E2E
- [Step 75](#step-75): **MILESTONE: Connectors working**

---

### **PHASE 6: Workflow Lifecycle** (Steps 76-85)
**Goal**: Add Draft/Publish/Archive workflow states
**Auth Mode**: `AllowLooseAuth=true`

- [Step 76](#step-76): Add Status column to Workflows table
- [Step 77](#step-77): Add checksum constraint to WorkflowDefinitions
- [Step 78](#step-78): Implement POST /workflows (create Draft)
- [Step 79](#step-79): Implement publish-time validation
- [Step 80](#step-80): Implement POST /workflows/{id}/publish
- [Step 81](#step-81): Implement POST /workflows/{id}/archive
- [Step 82](#step-82): Update sp_StartWorkflowExecution to check Status
- [Step 83](#step-83): Add autoActivate parameter support
- [Step 84](#step-84): Test lifecycle transitions
- [Step 85](#step-85): **MILESTONE: Lifecycle working**

---

### **PHASE 7: Principal & Auth** (Steps 86-92)
**Goal**: Add principal tracking and loose auth
**Auth Mode**: `AllowLooseAuth=true` (still loose, just tracking principals)

- [Step 86](#step-86): Add Principal columns to WorkflowExecutions
- [Step 87](#step-87): Update execute API to accept principal
- [Step 88](#step-88): Store principal in WorkflowExecutions
- [Step 89](#step-89): Add AuthOptions with AllowLooseAuth
- [Step 90](#step-90): Update logging to include principal fields
- [Step 91](#step-91): Test execute with principal
- [Step 92](#step-92): **MILESTONE: Principal tracking working**

---

### **PHASE 8: Resource Links & Idempotency** (Steps 93-100)
**Goal**: Add WorkflowResourceLinks for cross-run idempotency
**Auth Mode**: `AllowLooseAuth=true`

- [Step 93](#step-93): Create WorkflowResourceLinks table
- [Step 94](#step-94): Implement sp_LinkExternalResource
- [Step 95](#step-95): Implement sp_CheckResourceExists
- [Step 96](#step-96): Update action result to include ResourceLinks
- [Step 97](#step-97): Conductor persists ResourceLinks after action success
- [Step 98](#step-98): Update Monday action to return resource links
- [Step 99](#step-99): Test idempotency with duplicate execution
- [Step 100](#step-100): **MILESTONE: Idempotency working**

---

### **PHASE 9: Subworkflows** (Steps 101-108)
**Goal**: Add subworkflow node type
**Auth Mode**: `AllowLooseAuth=true`

- [Step 101](#step-101): Add WorkflowExecutionHierarchy table
- [Step 102](#step-102): Add nodeType field to Node definition
- [Step 103](#step-103): Implement subworkflow execution logic
- [Step 104](#step-104): Add ParentExecutionId foreign key
- [Step 105](#step-105): Implement waitForCompletion logic
- [Step 106](#step-106): Add MaxNestingDepth guardrail
- [Step 107](#step-107): Test parent → child workflow
- [Step 108](#step-108): **MILESTONE: Subworkflows working**

---

### **PHASE 10: Triggers** (Steps 109-118)
**Goal**: Add webhook, schedule, and manual triggers
**Auth Mode**: `AllowLooseAuth=true`

- [Step 109](#step-109): Create WorkflowTriggers table
- [Step 110](#step-110): Implement trigger CRUD API
- [Step 111](#step-111): Add webhook trigger handler
- [Step 112](#step-112): Register dynamic webhook endpoints
- [Step 113](#step-113): Implement ActivationCondition evaluation
- [Step 114](#step-114): Add Quartz.NET for schedule triggers
- [Step 115](#step-115): Implement schedule trigger handler
- [Step 116](#step-116): Test webhook trigger E2E
- [Step 117](#step-117): Test schedule trigger
- [Step 118](#step-118): **MILESTONE: Triggers working**

---

### **PHASE 11: Document Templates** (Steps 119-126)
**Goal**: Add template rendering system
**Auth Mode**: `AllowLooseAuth=true`

- [Step 119](#step-119): Create TemplateSets table
- [Step 120](#step-120): Create TemplateDefinitions table
- [Step 121](#step-121): Implement template CRUD API
- [Step 122](#step-122): Implement core.template.render action
- [Step 123](#step-123): Add POST /templates/{id}/render preview
- [Step 124](#step-124): Create sample template (project brief)
- [Step 125](#step-125): Test end-to-end template workflow
- [Step 126](#step-126): **MILESTONE: Templates working**

---

### **PHASE 12: Workflow Templates** (Steps 127-132)
**Goal**: Add workflow template instantiation
**Auth Mode**: `AllowLooseAuth=true`

- [Step 127](#step-127): Create WorkflowTemplates table
- [Step 128](#step-128): Implement template instantiation API
- [Step 129](#step-129): Add template rendering with config substitution
- [Step 130](#step-130): Create sample workflow template
- [Step 131](#step-131): Test template instantiation → Draft workflow
- [Step 132](#step-132): **MILESTONE: Workflow templates working**

---

### **PHASE 13: Observability** (Steps 133-142)
**Goal**: Add comprehensive logging, metrics, and tracing
**Auth Mode**: `AllowLooseAuth=true`

- [Step 133](#step-133): Configure Serilog with structured logging
- [Step 134](#step-134): Add standard log properties enricher
- [Step 135](#step-135): Create ExecutionEvents table
- [Step 136](#step-136): Log execution events to DB
- [Step 137](#step-137): Add OpenTelemetry ActivitySource
- [Step 138](#step-138): Add workflow/action spans
- [Step 139](#step-139): Add Prometheus metrics
- [Step 140](#step-140): Expose metrics endpoint
- [Step 141](#step-141): Test observability in local environment
- [Step 142](#step-142): **MILESTONE: Observability working**

---

### **PHASE 14: Queue & Background Runner** (Steps 143-150)
**Goal**: Move execution off API thread into background service
**Auth Mode**: `AllowLooseAuth=true`

- [Step 143](#step-143): Create DB outbox table
- [Step 144](#step-144): Update execute API to enqueue instead of execute
- [Step 145](#step-145): Implement BackgroundService runner
- [Step 146](#step-146): Add sp_TryAcquireExecution for concurrency
- [Step 147](#step-147): Implement outbox polling with SKIP LOCKED
- [Step 148](#step-148): Add DeadLetters table for DLQ
- [Step 149](#step-149): Test async execution
- [Step 150](#step-150): **MILESTONE: Background execution working**

---

### **PHASE 15: Advanced Features** (Steps 151-160)
**Goal**: Polish and production-readiness
**Auth Mode**: Step 157 **FLIPS** to `AllowLooseAuth=false` (strict JWT)

- [Step 151](#step-151): Add WorkflowPlans caching table
- [Step 152](#step-152): Implement plan caching
- [Step 153](#step-153): Add onFailure node routing
- [Step 154](#step-154): Add routePolicy (parallel/firstMatch)
- [Step 155](#step-155): Implement context snapshot pruning
- [Step 156](#step-156): Add rate limiting middleware
- [Step 157](#step-157): **FLIP AUTH: Enable strict JWT validation** ⚠️
- [Step 158](#step-158): Add multi-tenancy filtering
- [Step 159](#step-159): Add cancellation API
- [Step 160](#step-160): **MILESTONE: Production-ready!** 🚀

---

## Step Details

### Step 1: ALREADY DONE - Verify Existing Structure
**Reference**: §2 High-Level Architecture | Lines 38-88 (approx)

**Inputs**:
- Existing repository structure at `H:/Development/DataConnectors`
- Spec §2.1 Components

**Edits**: None (verification only)

**Checks**:
```bash
cd H:/Development/DataConnectors
ls src/  # Should show DataWorkflows.Engine, Connector projects
dotnet build  # Should succeed
```

**Existing Structure:**
```
DataConnectors/
├── DataWorkflows.sln                           EXISTS
├── src/
│   ├── DataWorkflows.Engine/                   EXISTS (Web API + Orchestrator stub)
│   ├── DataWorkflows.Contracts/                EXISTS (Shared DTOs)
│   ├── DataWorkflows.Connector.Monday/         EXISTS
│   ├── DataWorkflows.Connector.Slack/          EXISTS
│   ├── DataWorkflows.Connector.Confluence/     EXISTS
│   ├── DataWorkflows.Connector.Outlook/        EXISTS
│   ├── DataWorkflows.Connector.TaskTracker/    EXISTS
│   ├── DataWorkflows.SlackBot/                 EXISTS
│   └── DataWorkflows.TaskTracker.MockApi/      EXISTS
└── tests/
    ├── DataWorkflows.Connector.Monday.Tests/           EXISTS
    └── DataWorkflows.Connector.Monday.IntegrationTests/ EXISTS
```

**Current State of DataWorkflows.Engine:**
- ASP.NET Core 8.0 Web API
- Polly, Dapper, Npgsql already referenced
- Swagger configured
- `IWorkflowOrchestrator` interface defined
- `WorkflowOrchestrator` stub implementation (TODO placeholder)
- `WorkflowController` with POST /{id}/execute endpoint
- Health check endpoint
- DI container configured
- Clean architecture folders: Core/Application/, Core/Interfaces/, Presentation/Controllers/

**Expected Result**: Solution builds, all projects compile

---

### Step 2: Add Missing Dependencies to DataWorkflows.Engine
**Reference**: §2.1 Components | Lines 42-88 (approx), Template Engine §5 | Lines 784-815 (approx), Condition Evaluator §6 | Lines 817-843 (approx)

**Inputs**:
- Existing DataWorkflows.Engine.csproj
- Spec §5 Template Engine (Scriban requirements)
- Spec §6 Condition Evaluator (Jint requirements)

**Edits**:
- `src/DataWorkflows.Engine/DataWorkflows.Engine.csproj` (modify)

**Checks**:
```bash
cd src/DataWorkflows.Engine
dotnet restore
dotnet build  # Should succeed with no errors
dotnet list package  # Should show FluentValidation 11.*, Scriban 5.*, Jint 3.*, Serilog.AspNetCore 8.*
```

**Already Has:**
- Polly 8.5.0
- Dapper 2.1.35
- Npgsql 8.0.3

**Add These:**
```bash
cd src/DataWorkflows.Engine
dotnet add package FluentValidation --version 11.*
dotnet add package Scriban --version 5.*
dotnet add package Jint --version 3.*
dotnet add package Serilog.AspNetCore --version 8.*
```

**Update DataWorkflows.Engine.csproj to include:**
```xml
<PackageReference Include="FluentValidation" Version="11.*" />
<PackageReference Include="Scriban" Version="5.*" />
<PackageReference Include="Jint" Version="3.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
```

**Expected Result**: All packages restore, solution builds successfully

---

### Step 3: Configure DI Container and appsettings
**Reference**: §10 Options | Lines 1127-1196 (approx), §12.5.4 Auth Options | Lines 1324-1342 (approx)

**Inputs**:
- Existing `src/DataWorkflows.Engine/Program.cs`
- Existing `src/DataWorkflows.Engine/appsettings.json`
- Spec §10.1-10.6 for all options classes

**Edits**:
- `src/DataWorkflows.Engine/appsettings.json` (modify)
- `src/DataWorkflows.Engine/Program.cs` (modify - add health checks)

**Checks**:
```bash
dotnet run --project src/DataWorkflows.Engine
# App should start without errors
curl http://localhost:5131/health  # Should return 200 OK with "Healthy"
```

**Update:** `src/DataWorkflows.Engine/appsettings.json` to add:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=dataworkflows;Username=postgres;Password=postgres"
  },
  "Orchestration": {
    "MaxParallelActions": 10,
    "DefaultActionTimeout": "00:05:00",
    "DefaultWorkflowTimeout": "01:00:00"
  },
  "Auth": {
    "AllowLooseAuth": true,
    "RequirePrincipalForExecute": false
  }
}
```

**Add Health Check to Program.cs** (after existing service registrations):
```csharp
builder.Services.AddHealthChecks();
```

**Map health endpoint** (before app.Run()):
```csharp
app.MapHealthChecks("/health");
```

**Expected Result**: App starts, /health endpoint returns `Healthy`, no errors in console

---

### Step 4: DONE IN STEP 3 - Health Check Added

---

### Step 5: Verify Solution Builds and Runs
**Reference**: §14.4 Health Checks | Lines 1611-1617 (approx)

**Inputs**:
- Completed Steps 1-3
- All project files in solution

**Edits**: None (verification only)

**Checks**:
```bash
cd H:/Development/DataConnectors
dotnet clean
dotnet build  # Should succeed with 0 errors
dotnet test  # Monday connector tests should pass
dotnet run --project src/DataWorkflows.Engine  # Should start on port 5131

# In separate terminal:
curl http://localhost:5131/health  # Should return 200 OK
curl http://localhost:5131/swagger  # Should return Swagger UI HTML

# Open browser:
http://localhost:5131/swagger  # Should show API documentation with /workflows/{id}/execute endpoint
```

**Expected Result**:
- Build succeeds with 0 errors
- Tests pass (Monday connector tests exist)
- Engine API starts on http://localhost:5131
- Swagger UI shows workflow/execute endpoint
- /health returns `Healthy` status

---

### Step 6: Create Workflows Table (Minimal)
**Reference**: §3.1 Core Workflow Tables | Lines 131-142 (approx), §3.0 Workflow Lifecycle | Lines 94-128 (approx)

**Inputs**:
- Spec §3.1 SQL schema for Workflows table
- Docker Compose PostgreSQL instance (verify running)
- Connection string from appsettings.json

**Edits**:
- `src/DataWorkflows.Data/Migrations/001_CreateWorkflows.sql` (create)

**Checks**:
```bash
# Verify Postgres is running
docker ps | grep postgres

# Run migration manually (automated migrations in later phase)
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/001_CreateWorkflows.sql

# Verify table exists
psql -h localhost -U postgres -d dataworkflows -c "\d Workflows"
# Should show: Id, DisplayName, CurrentVersion, Status, IsEnabled, CreatedAt columns

# Verify constraint
psql -h localhost -U postgres -d dataworkflows -c "INSERT INTO Workflows (Id, DisplayName, Status) VALUES ('test', 'Test', 'InvalidStatus');"
# Should fail with CHECK constraint violation
```

**Create:** `src/DataWorkflows.Data/Migrations/001_CreateWorkflows.sql`
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

**Expected Result**: Table created in Postgres, constraints enforced

---

### Step 7: Create WorkflowDefinitions Table
**Reference**: §3.1 Core Workflow Tables | Lines 144-154 (approx)

**Inputs**:
- Spec §3.1 SQL schema for WorkflowDefinitions
- Existing Workflows table from Step 6

**Edits**:
- `src/DataWorkflows.Data/Migrations/002_CreateWorkflowDefinitions.sql` (create)

**Checks**:
```bash
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/002_CreateWorkflowDefinitions.sql

# Verify table exists
psql -h localhost -U postgres -d dataworkflows -c "\d WorkflowDefinitions"
# Should show: WorkflowId, Version, DefinitionJson, Checksum, CreatedAt

# Verify foreign key
psql -h localhost -U postgres -d dataworkflows -c "INSERT INTO WorkflowDefinitions (WorkflowId, Version, DefinitionJson, Checksum) VALUES ('nonexistent', 1, '{}'::jsonb, 'abc');"
# Should fail with foreign key violation

# Verify unique constraint
psql -h localhost -U postgres -d dataworkflows -c "INSERT INTO Workflows (Id, DisplayName) VALUES ('test-wf', 'Test');"
psql -h localhost -U postgres -d dataworkflows -c "INSERT INTO WorkflowDefinitions (WorkflowId, Version, DefinitionJson, Checksum) VALUES ('test-wf', 1, '{}'::jsonb, 'abc123');"
psql -h localhost -U postgres -d dataworkflows -c "INSERT INTO WorkflowDefinitions (WorkflowId, Version, DefinitionJson, Checksum) VALUES ('test-wf', 2, '{}'::jsonb, 'abc123');"
# Second insert should fail (duplicate checksum for same workflow)
```

**Create:** `src/DataWorkflows.Data/Migrations/002_CreateWorkflowDefinitions.sql`
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

**Expected Result**: Table created with composite PK and checksum uniqueness enforced

---

### Step 8: Create WorkflowExecutions Table
**Reference**: §3.1 Core Workflow Tables | Lines 156-186 (approx)

**Inputs**:
- Spec §3.1 SQL schema for WorkflowExecutions
- Existing WorkflowDefinitions table from Step 7
- Note: Principal columns added in Phase 7 (Step 86)

**Edits**:
- `src/DataWorkflows.Data/Migrations/003_CreateWorkflowExecutions.sql` (create)

**Checks**:
```bash
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/003_CreateWorkflowExecutions.sql

# Verify table exists
psql -h localhost -U postgres -d dataworkflows -c "\d WorkflowExecutions"
# Should show: Id (UUID), WorkflowId, WorkflowVersion, WorkflowRequestId, Status, TriggerPayloadJson, etc.

# Verify unique index on (WorkflowId, WorkflowRequestId)
psql -h localhost -U postgres -d dataworkflows -c "
  INSERT INTO WorkflowDefinitions (WorkflowId, Version, DefinitionJson, Checksum)
  VALUES ('exec-test', 1, '{}'::jsonb, 'hash1');
"
psql -h localhost -U postgres -d dataworkflows -c "
  INSERT INTO WorkflowExecutions (WorkflowId, WorkflowVersion, WorkflowRequestId, Status, TriggerPayloadJson)
  VALUES ('exec-test', 1, 'req-123', 'Pending', '{}'::jsonb);
"
psql -h localhost -U postgres -d dataworkflows -c "
  INSERT INTO WorkflowExecutions (WorkflowId, WorkflowVersion, WorkflowRequestId, Status, TriggerPayloadJson)
  VALUES ('exec-test', 1, 'req-123', 'Pending', '{}'::jsonb);
"
# Second insert should fail (duplicate WorkflowRequestId for same workflow)
```

**Create:** `src/DataWorkflows.Data/Migrations/003_CreateWorkflowExecutions.sql`
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

**Expected Result**: Table + unique index created, idempotency enforced

---

### Step 9: Create ActionExecutions Table
**Reference**: §3.1 Core Workflow Tables | Lines 189-207 (approx)

**Inputs**:
- Spec §3.1 SQL schema for ActionExecutions
- Existing WorkflowExecutions table from Step 8

**Edits**:
- `src/DataWorkflows.Data/Migrations/004_CreateActionExecutions.sql` (create)

**Checks**:
```bash
psql -h localhost -U postgres -d dataworkflows -f src/DataWorkflows.Data/Migrations/004_CreateActionExecutions.sql

# Verify table exists
psql -h localhost -U postgres -d dataworkflows -c "\d ActionExecutions"
# Should show: Id, WorkflowExecutionId, NodeId, ActionType, Status, Attempt, OutputsJson, ErrorJson, etc.

# Verify index
psql -h localhost -U postgres -d dataworkflows -c "\di ix_actionexec_by_exec_node"
# Should show index on (WorkflowExecutionId, NodeId)

# Test cascade delete
psql -h localhost -U postgres -d dataworkflows -c "
  INSERT INTO WorkflowExecutions (Id, WorkflowId, WorkflowVersion, WorkflowRequestId, Status, TriggerPayloadJson)
  VALUES ('11111111-1111-1111-1111-111111111111', 'exec-test', 1, 'cascade-test', 'Succeeded', '{}'::jsonb);
"
psql -h localhost -U postgres -d dataworkflows -c "
  INSERT INTO ActionExecutions (WorkflowExecutionId, NodeId, ActionType, Status)
  VALUES ('11111111-1111-1111-1111-111111111111', 'node1', 'core.echo', 'Succeeded');
"
psql -h localhost -U postgres -d dataworkflows -c "DELETE FROM WorkflowExecutions WHERE Id = '11111111-1111-1111-1111-111111111111';"
psql -h localhost -U postgres -d dataworkflows -c "SELECT * FROM ActionExecutions WHERE WorkflowExecutionId = '11111111-1111-1111-1111-111111111111';"
# Should return 0 rows (cascade delete worked)
```

**Create:** `src/DataWorkflows.Data/Migrations/004_CreateActionExecutions.sql`
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

**Expected Result**: Table + index created, cascade delete works

---

### Step 10: Add Dapper Repositories
**Reference**: §2.1 Persistence Layer | Line 79 (approx)

**Inputs**:
- Spec §2.1 persistence design (Dapper + stored procedures)
- Existing tables from Steps 6-9
- Connection string from appsettings.json

**Edits**:
- `src/DataWorkflows.Data/Repositories/WorkflowExecutionRepository.cs` (create)
- `src/DataWorkflows.Data/Repositories/ActionExecutionRepository.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Data  # Should compile without errors

# In C# REPL or test:
var repo = new WorkflowExecutionRepository("Host=localhost;Database=dataworkflows;Username=postgres;Password=postgres");
var execId = await repo.CreateExecution("exec-test", 1, "test-req-456", "{}");
Console.WriteLine(execId);  // Should print a GUID
```

**Create:** `src/DataWorkflows.Data/Repositories/WorkflowExecutionRepository.cs`
```csharp
using Dapper;
using Npgsql;
using System.Data;

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

  // More methods added in later steps
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

**Create:** `src/DataWorkflows.Data/Repositories/ActionExecutionRepository.cs`
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

**Expected Result**: Repositories compile, can insert/query executions

---

### Step 11: Define WorkflowDefinition Model
**Reference**: §4.1 Workflow Definition Schema | Lines 642-757 (approx)

**Inputs**:
- Spec §4.1 complete JSON schema
- C# record types for immutability

**Edits**:
- `src/DataWorkflows.Engine/Models/WorkflowDefinition.cs` (create)
- `src/DataWorkflows.Engine/Models/Node.cs` (create)
- `src/DataWorkflows.Engine/Models/Edge.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine  # Should compile

# In C# REPL:
var workflow = new WorkflowDefinition(
  Id: "test",
  DisplayName: "Test",
  StartNode: "node1",
  Nodes: new List<Node> {
    new Node(Id: "node1", ActionType: "core.echo")
  }
);
Console.WriteLine(workflow.StartNode);  // Should print "node1"
```

**Create:** `src/DataWorkflows.Engine/Models/WorkflowDefinition.cs`
```csharp
namespace DataWorkflows.Engine.Models;

public sealed record WorkflowDefinition(
  string Id,
  string DisplayName,
  string StartNode,
  List<Node> Nodes
);
```

**Create:** `src/DataWorkflows.Engine/Models/Node.cs`
```csharp
namespace DataWorkflows.Engine.Models;

public sealed record Node(
  string Id,
  string ActionType,
  Dictionary<string, object>? Parameters = null,
  List<Edge>? Edges = null
);
```

**Create:** `src/DataWorkflows.Engine/Models/Edge.cs`
```csharp
namespace DataWorkflows.Engine.Models;

public sealed record Edge(
  string TargetNode,
  string When = "success",
  string? Condition = null
);
```

**Expected Result**: Models compile, can instantiate workflow definition

---

### Step 12: Add JSON Deserializer
**Reference**: §4.1 Workflow Definition Schema | Lines 642-757 (approx)

**Inputs**:
- Models from Step 11
- System.Text.Json library (already in .NET 8)

**Edits**:
- `src/DataWorkflows.Engine/Parsing/WorkflowParser.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine

# Test parsing:
var json = """
{
  "id": "test",
  "displayName": "Test Workflow",
  "startNode": "echo1",
  "nodes": [
    { "id": "echo1", "actionType": "core.echo", "parameters": { "message": "Hello" } }
  ]
}
""";
var parser = new WorkflowParser();
var workflow = parser.Parse(json);
Console.WriteLine(workflow.Nodes[0].ActionType);  // Should print "core.echo"
```

**Create:** `src/DataWorkflows.Engine/Parsing/WorkflowParser.cs`
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

**Expected Result**: Can parse JSON to WorkflowDefinition object

---

### Step 13: Create IWorkflowAction Interface
**Reference**: §7.1-7.4 Action Contract | Lines 849-901 (approx)

**Inputs**:
- Spec §7 complete action contract design
- C# async/await patterns

**Edits**:
- `src/DataWorkflows.Contracts/Actions/IWorkflowAction.cs` (create)
- `src/DataWorkflows.Contracts/Actions/ActionExecutionContext.cs` (create)
- `src/DataWorkflows.Contracts/Actions/ActionExecutionResult.cs` (create)
- `src/DataWorkflows.Contracts/Actions/ActionExecutionStatus.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Contracts  # Should compile
dotnet build  # Entire solution should build
```

**Create:** `src/DataWorkflows.Contracts/Actions/IWorkflowAction.cs`
```csharp
namespace DataWorkflows.Contracts.Actions;

public interface IWorkflowAction {
  string Type { get; }
  Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}
```

**Create:** `src/DataWorkflows.Contracts/Actions/ActionExecutionContext.cs`
```csharp
namespace DataWorkflows.Contracts.Actions;

public sealed record ActionExecutionContext(
  Guid WorkflowExecutionId,
  string NodeId,
  Dictionary<string, object?> Parameters,
  IServiceProvider Services
);
```

**Create:** `src/DataWorkflows.Contracts/Actions/ActionExecutionResult.cs`
```csharp
namespace DataWorkflows.Contracts.Actions;

public sealed record ActionExecutionResult(
  ActionExecutionStatus Status,
  Dictionary<string, object?> Outputs,
  string? ErrorMessage = null
);
```

**Create:** `src/DataWorkflows.Contracts/Actions/ActionExecutionStatus.cs`
```csharp
namespace DataWorkflows.Contracts.Actions;

public enum ActionExecutionStatus {
  Succeeded,
  Failed,
  RetriableFailure,
  Skipped
}
```

**Expected Result**: Interfaces compile, can be implemented

---

### Step 14: Implement core.echo Action
**Reference**: §7.7 Built-in Actions | Line 955 (approx)

**Inputs**:
- IWorkflowAction interface from Step 13
- Spec §7.7 for core.echo behavior

**Edits**:
- `src/DataWorkflows.Engine/Actions/CoreEchoAction.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine

# Test action:
var action = new CoreEchoAction();
var context = new ActionExecutionContext(
  Guid.NewGuid(),
  "test-node",
  new Dictionary<string, object?> { ["message"] = "Hello World" },
  null!
);
var result = await action.ExecuteAsync(context, CancellationToken.None);
Console.WriteLine(result.Outputs["echo"]);  // Should print "Hello World"
```

**Create:** `src/DataWorkflows.Engine/Actions/CoreEchoAction.cs`
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

**Expected Result**: Action compiles, executes synchronously, returns output

---

### Step 15: Add Action Registry
**Reference**: §7.5 Registry & DI | Lines 903-913 (approx)

**Inputs**:
- IWorkflowAction interface from Step 13
- CoreEchoAction from Step 14

**Edits**:
- `src/DataWorkflows.Engine/Registry/ActionRegistry.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine

# Test registry:
var registry = new ActionRegistry();
var action = registry.GetAction("core.echo");
Console.WriteLine(action.Type);  // Should print "core.echo"

# Test missing action:
try {
  registry.GetAction("nonexistent");
} catch (KeyNotFoundException ex) {
  Console.WriteLine(ex.Message);  // Should print "Action not found: nonexistent"
}
```

**Create:** `src/DataWorkflows.Engine/Registry/ActionRegistry.cs`
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

**Expected Result**: Registry compiles, can register/retrieve actions

---

### Step 16: Create WorkflowContext
**Reference**: §8.1 Thread-Safe Store | Lines 964-984 (approx)

**Inputs**:
- Spec §8.1 for thread-safe context design
- System.Collections.Concurrent namespace

**Edits**:
- `src/DataWorkflows.Engine/Execution/WorkflowContext.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine

# Test context:
var context = new WorkflowContext();
context.SetActionOutput("node1", new { result = "success" });
var outputs = context.GetAllOutputs();
Console.WriteLine(outputs["node1"]);  // Should print output object
```

**Create:** `src/DataWorkflows.Engine/Execution/WorkflowContext.cs`
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

**Expected Result**: Context compiles, thread-safe output storage works

---

### Step 17: Create WorkflowConductor (Linear Execution)
**Reference**: §9.2 Run Loop | Lines 1076-1087 (approx) - **IGNORE parallelism/retries for now**

**Inputs**:
- WorkflowDefinition from Step 11
- ActionRegistry from Step 15
- WorkflowContext from Step 16
- Spec §9.2 execution loop (simplified for Phase 1)

**Edits**:
- `src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine

# Test conductor:
var workflow = new WorkflowDefinition(
  "test", "Test", "node1",
  new List<Node> {
    new Node("node1", "core.echo", new Dictionary<string, object?> { ["message"] = "First" }),
    new Node("node2", "core.echo", new Dictionary<string, object?> { ["message"] = "Second" })
  }
);
var conductor = new WorkflowConductor(new ActionRegistry());
await conductor.ExecuteAsync(workflow, new Dictionary<string, object>());
// Should complete without errors
```

**Create:** `src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs`
```csharp
using DataWorkflows.Contracts.Actions;
using DataWorkflows.Engine.Execution;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Registry;

namespace DataWorkflows.Engine.Orchestration;

public class WorkflowConductor {
  private readonly ActionRegistry _registry;

  public WorkflowConductor(ActionRegistry registry) {
    _registry = registry;
  }

  public async Task ExecuteAsync(WorkflowDefinition workflow, Dictionary<string, object> trigger) {
    var context = new WorkflowContext();

    // Simple linear execution (ignore edges for now)
    foreach (var node in workflow.Nodes) {
      var action = _registry.GetAction(node.ActionType);

      var actionContext = new ActionExecutionContext(
        WorkflowExecutionId: Guid.NewGuid(),
        NodeId: node.Id,
        Parameters: node.Parameters ?? new(),
        Services: null! // Fix in later step
      );

      var result = await action.ExecuteAsync(actionContext, CancellationToken.None);

      if (result.Status == ActionExecutionStatus.Succeeded) {
        context.SetActionOutput(node.Id, result.Outputs);
      } else {
        throw new Exception($"Action failed: {result.ErrorMessage}");
      }
    }
  }
}
```

**Expected Result**: Can execute 2-node workflow in memory

---

### Step 18: Execute Nodes Sequentially
**Already implemented in Step 17**

---

### Step 19: Record ActionExecutions to DB
**Reference**: §3.1 ActionExecutions Table | Lines 189-207 (approx)

**Inputs**:
- WorkflowConductor from Step 17
- ActionExecutionRepository from Step 10
- Connection string from appsettings.json

**Edits**:
- `src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs` (modify)

**Checks**:
```bash
# After running conductor:
psql -h localhost -U postgres -d dataworkflows -c "SELECT * FROM ActionExecutions ORDER BY StartTime DESC LIMIT 5;"
# Should show recent action executions with NodeId, ActionType, Status, OutputsJson
```

**Update:** `WorkflowConductor.ExecuteAsync` method:
```csharp
public async Task ExecuteAsync(
  Guid executionId,
  WorkflowDefinition workflow,
  Dictionary<string, object> trigger,
  string connectionString
) {
  var context = new WorkflowContext();
  var actionRepo = new ActionExecutionRepository(connectionString);

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
}
```

**Expected Result**: ActionExecutions rows appear in DB after workflow runs

---

### Step 20: Return Execution Result
**Reference**: §9.2 Run Loop | Lines 1076-1087 (approx)

**Inputs**:
- WorkflowConductor from Step 19

**Edits**:
- `src/DataWorkflows.Engine/Orchestration/WorkflowConductor.cs` (modify)
- `src/DataWorkflows.Engine/Models/ExecutionResult.cs` (create)

**Checks**:
```bash
# Test in C#:
var result = await conductor.ExecuteAsync(...);
Console.WriteLine($"Execution {result.ExecutionId} completed with status {result.Status}");
```

**Update:** `WorkflowConductor.ExecuteAsync` return type:
```csharp
public async Task<ExecutionResult> ExecuteAsync(...) {
  // ... execution logic from Step 19 ...

  return new ExecutionResult(
    ExecutionId: executionId,
    Status: "Succeeded",
    CompletedAt: DateTime.UtcNow
  );
}
```

**Create:** `src/DataWorkflows.Engine/Models/ExecutionResult.cs`
```csharp
namespace DataWorkflows.Engine.Models;

public record ExecutionResult(
  Guid ExecutionId,
  string Status,
  DateTime CompletedAt
);
```

**Expected Result**: Caller receives execution summary

---

### Step 21: Create ExecuteController
**Reference**: §13.1 Workflow Execution | Lines 1349-1378 (approx)

**Inputs**:
- WorkflowParser from Step 12
- WorkflowConductor from Step 20
- ActionRegistry from Step 15
- Connection string from appsettings.json

**Edits**:
- `src/DataWorkflows.Engine/Controllers/ExecuteController.cs` (create)
- `src/DataWorkflows.Engine/Models/ExecuteRequest.cs` (create)

**Checks**:
```bash
dotnet build src/DataWorkflows.Engine
dotnet run --project src/DataWorkflows.Engine

# Test endpoint:
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d '{"trigger": {"source": "test"}}'
# Should return 202 Accepted with executionId
```

**Create:** `src/DataWorkflows.Engine/Controllers/ExecuteController.cs`
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
    // Hardcoded workflow for MVP (Step 26 will use DB)
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

**Create:** `src/DataWorkflows.Engine/Models/ExecuteRequest.cs`
```csharp
namespace DataWorkflows.Engine.Models;

public record ExecuteRequest(Dictionary<string, object>? Trigger);
```

**Expected Result**: POST /execute returns execution ID and status URL

---

### Step 22: Parse Workflow from JSON
**Already implemented in Step 21 (hardcoded workflow JSON for Phase 1)**

---

### Step 23: Call Conductor, Return ID
**Already implemented in Step 21**

---

### Step 24: Add GET /executions/{id}
**Reference**: §13.2 Execution Queries | Lines 1380-1388 (approx)

**Inputs**:
- WorkflowExecutionRepository from Step 10

**Edits**:
- `src/DataWorkflows.Engine/Controllers/ExecutionsController.cs` (create)

**Checks**:
```bash
# First create an execution:
EXEC_ID=$(curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d '{"trigger": {}}' | jq -r '.executionId')

# Then query it:
curl http://localhost:5131/api/v1/executions/$EXEC_ID
# Should return execution details (WorkflowId, Status, StartTime, etc.)
```

**Create:** `src/DataWorkflows.Engine/Controllers/ExecutionsController.cs`
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

**Expected Result**: Can query execution status via GET /executions/{id}

---

### Step 25: Test with Postman/curl
**Reference**: Manual testing procedures

**Inputs**:
- Running DataWorkflows.Engine API (from Step 21)
- curl or Postman

**Edits**: None (testing only)

**Checks**:
```bash
# Test execute endpoint:
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d '{"trigger": {"source": "manual", "userId": "test-user"}}' \
  | jq .

# Expected response:
{
  "executionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Succeeded",
  "statusUrl": "/api/v1/executions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}

# Test query endpoint:
curl http://localhost:5131/api/v1/executions/{executionId-from-above} | jq .

# Expected response:
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "workflowId": "test",
  "workflowVersion": 1,
  "status": "Succeeded",
  "triggerPayloadJson": "{\"source\":\"manual\",\"userId\":\"test-user\"}",
  ...
}
```

**Expected Result**: Both endpoints work, execution persists in DB

---

### Step 26: Create Test Workflow JSON Fixture
**Reference**: §15.1 Simple Linear Workflow | Lines 1623-1665 (approx)

**Inputs**:
- Spec §15.1 workflow example
- Fixtures directory structure (create if needed)

**Edits**:
- `fixtures/phase1/simple-echo-workflow.json` (create)
- `fixtures/phase1/execute-request.json` (create)

**Checks**:
```bash
cat fixtures/phase1/simple-echo-workflow.json  # Should show valid JSON
cat fixtures/phase1/execute-request.json  # Should show valid trigger payload
```

**Create:** `fixtures/phase1/simple-echo-workflow.json`
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

**Create:** `fixtures/phase1/execute-request.json`
```json
{
  "trigger": {
    "source": "manual",
    "userId": "test-user-123",
    "timestamp": "2025-01-15T10:00:00Z"
  }
}
```

**Expected Result**: Fixture files created, valid JSON

---

### Step 27: Execute via API
**Reference**: §13.1 Workflow Execution | Lines 1349-1378 (approx)

**Inputs**:
- Fixtures from Step 26
- Running API from Step 21

**Edits**: None (testing only)

**Checks**:
```bash
# Execute workflow with fixture:
curl -X POST http://localhost:5131/api/v1/workflows/simple-echo/execute \
  -H "Content-Type: application/json" \
  -d @fixtures/phase1/execute-request.json \
  | jq '.' | tee /tmp/execution-response.json

# Save executionId for next steps:
EXEC_ID=$(jq -r '.executionId' /tmp/execution-response.json)
echo "Execution ID: $EXEC_ID"

# Verify accepted:
# Expected HTTP 202 Accepted with executionId in response
```

**Expected Result**: Returns 202 Accepted with executionId

---

### Step 28: Query Execution Status
**Reference**: §13.2 Execution Queries | Lines 1380-1388 (approx)

**Inputs**:
- Execution ID from Step 27

**Edits**: None (testing only)

**Checks**:
```bash
# Query execution (use EXEC_ID from Step 27):
curl http://localhost:5131/api/v1/executions/$EXEC_ID | jq '.'

# Expected response fields:
{
  "id": "...",
  "workflowId": "simple-echo",
  "workflowVersion": 1,
  "status": "Succeeded",
  "triggerPayloadJson": "{\"source\":\"manual\",\"userId\":\"test-user-123\",...}",
  "startTime": "2025-01-15T10:00:01Z",
  "endTime": "2025-01-15T10:00:02Z",
  "correlationId": "..."
}
```

**Expected Result**: Returns execution with Status="Succeeded"

---

### Step 29: Verify ActionExecutions in DB
**Reference**: §3.1 ActionExecutions Table | Lines 189-207 (approx)

**Inputs**:
- Execution ID from Step 27
- PostgreSQL connection

**Edits**: None (verification only)

**Checks**:
```bash
# Query ActionExecutions for the workflow execution:
psql -h localhost -U postgres -d dataworkflows -c "
  SELECT
    NodeId,
    ActionType,
    Status,
    OutputsJson,
    StartTime,
    EndTime
  FROM ActionExecutions
  WHERE WorkflowExecutionId = '$EXEC_ID'
  ORDER BY StartTime;
"

# Expected output:
#  nodeid | actiontype | status    | outputsjson              | starttime           | endtime
# --------+------------+-----------+--------------------------+---------------------+---------------------
#  echo1  | core.echo  | Succeeded | {"echo":"Hello from..."} | 2025-01-15 10:00:01 | 2025-01-15 10:00:01
#  echo2  | core.echo  | Succeeded | {"echo":"Hello from..."} | 2025-01-15 10:00:01 | 2025-01-15 10:00:02
```

**Expected Result**: Shows 2 ActionExecutions, both Succeeded, correct outputs

---

### Step 30: MILESTONE - Working E2E! 🎉
**Reference**: Phase 1 completion

**Inputs**:
- Completed Steps 1-29

**Edits**: None (celebration!)

**Checks**:
```bash
# Full E2E test:
curl -X POST http://localhost:5131/api/v1/workflows/test/execute \
  -H "Content-Type: application/json" \
  -d @fixtures/phase1/execute-request.json | jq -r '.executionId' | \
  xargs -I {} curl http://localhost:5131/api/v1/executions/{} | jq '.'

# Should show completed workflow execution

# Verify solution still builds:
dotnet build
# Should succeed with 0 errors

# Verify health check:
curl http://localhost:5131/health
# Should return "Healthy"
```

**Achievements:**
- ✅ 2-node linear workflow executes via API
- ✅ Workflow definition parsed from JSON
- ✅ Actions execute sequentially
- ✅ Results stored in PostgreSQL
- ✅ Execution queryable via API
- ✅ All core components integrated

**What We Haven't Built Yet:**
- ❌ Retries (Phase 2)
- ❌ Branching/conditions (Phase 3)
- ❌ Templating (Phase 4)
- ❌ Real connectors (Phase 5)
- ❌ Lifecycle management (Phase 6)
- ❌ Auth (Phase 7)
- ❌ Everything else!

**Next Phase**: Add retry policies and error handling

---

## Phases 2-15 Summary

The remaining phases follow the same detailed structure:

- **Phase 2**: Retries with Polly, transient vs permanent failures
- **Phase 3**: Jint conditions, graph validation, parallel execution
- **Phase 4**: Scriban templating, parameter binding
- **Phase 5**: Monday/Slack connectors, env var credentials
- **Phase 6**: Draft/Publish/Archive lifecycle
- **Phase 7**: Principal tracking, loose auth mode
- **Phase 8**: Resource links, cross-run idempotency
- **Phase 9**: Subworkflows, hierarchy tracking
- **Phase 10**: Triggers (webhook, schedule, manual)
- **Phase 11**: Document template rendering
- **Phase 12**: Workflow template instantiation
- **Phase 13**: Serilog, OpenTelemetry, Prometheus
- **Phase 14**: Background runner, outbox pattern
- **Phase 15**: JWT auth flip, rate limiting, production hardening

Each phase has ~8-12 steps with the same structure:
- Inputs (spec refs, files to read)
- Edits (exact paths)
- Checks (build/curl/DB queries)

---

## How to Use This Plan with LLMs

### For Each Step, Provide:

1. **The step details** (from this document)
2. **Referenced spec sections** (copy relevant lines from workflow_engine_spec.md)
3. **Existing file contents** (if modifying)
4. **Clear instruction**: "Implement Step X exactly as specified"

### Example Prompt Template:

```
I'm implementing Step 14 of the DataWorkflows engine plan.

STEP DETAILS:
[Paste Step 14 content from this file]

SPEC REFERENCE:
[Paste §7.7 Built-in Actions from workflow_engine_spec.md]

INSTRUCTION:
Implement this step exactly as specified. Create the CoreEchoAction class with the exact signature shown. Verify the code compiles.
```

### Context Window Management:

- **Don't send entire plan** (981 lines)
- **Do send**:
  - Current step (~50 lines)
  - Referenced spec section (~20-50 lines)
  - Existing file if modifying (~100 lines)
- **Total**: ~200 lines per step (well under most context limits)

### Verification After Each Step:

```bash
# Always run after LLM generates code:
dotnet build  # Must succeed
dotnet test   # Must pass
# Run step-specific Checks section commands
```

---

## Auth Mode Progression (Critical!)

**Phases 1-6, 8-14**: `AllowLooseAuth=true`
- No JWT required
- Principal optional
- Env var credentials
- **DO NOT implement strict auth yet**

**Phase 7**: Principal tracking (still loose auth)
- Adds principal fields
- Still no JWT enforcement

**Phase 15, Step 157**: **FLIP TO STRICT MODE**
- Change appsettings.json: `AllowLooseAuth: false`
- Enable JWT validation
- Require principal
- Add Vault-based credentials (future)

**Warning**: Do not implement JWT validation before Step 157, even if an LLM suggests it!

---

## Total Implementation Scope

- **160 steps** across 15 phases
- **~40-60 hours** solo development (assuming 15-20 min/step)
- **Each phase** independently valuable
- **Pause points** at every milestone (Steps 30, 40, 50, etc.)

**Remember**: The goal is **working software at every milestone**, not perfection.

---

**END OF IMPLEMENTATION PLAN**
