# Workflow Planning Notes

## Drivers
- Centralize project data around Monday items and maintain cross-system links (Slack, Confluence, TaskTracker).
- Support both on-demand (Slack bot / future GUI) and scheduled execution paths.
- Enable conditional, branched workflows with reusable, code-defined actions parameterized by configuration.

## Workflow Model
- Represent workflows as directed acyclic graphs (DAGs) where nodes are typed actions and edges define ordering and conditions.
- Each action implements `ExecuteAsync(context, parameters) -> result` and should be idempotent.
- Shared execution context stores Monday item data plus created resource identifiers to feed later steps.
- Configuration maps Monday columns and other per-board values into the parameters required by each action.

## Configuration Strategy
- MVP: store workflow definitions as JSON/YAML in Git for easy versioning.
- Plan future migration to PostgreSQL tables (`workflow`, `workflow_version`, `workflow_node`, `workflow_edge`, `workflow_parameter`).
- Keep configuration schema strict (prefer JSON schema) to validate field mappings and expressions before runtime.

## Triggers & Runtime
- Slack bot sends workflow id + trigger payload (e.g., Monday item link) to the Workflow Engine API.
- Future schedulers or event sources can enqueue requests backed by the same execution pipeline.
- Runtime loads workflow definition, materializes the DAG, and orchestrates action execution including conditional routing and join nodes for parallel branches.

## Idempotency & State
- Accept an external `workflow_request_id` (from Slack/UI) and persist `workflow_execution` records keyed by request id to prevent duplicate processing.
- Maintain `workflow_action_execution` rows capturing parameters, status, external resource ids, and timestamps.
- For cross-system links, rely on both Monday columns and persistence in `workflow_link` tables to make subsequent runs aware of existing resources.
- Actions should verify remote state before creation (e.g., check Monday link field, query Confluence for known page id) and update both Monday and the execution context when creating new artifacts.

## Resilience & Retries
- Each connector wraps outbound calls with Polly policies (retry + exponential backoff, circuit breakers where appropriate).
- Workflow Engine can apply overall retry policies per action, surfacing final failures with detailed logs while avoiding Slack-driven manual resumes.
- Execution logs (success/failure, retry counts) flow into PostgreSQL for audit and follow-up.

## Branching & Parallelism
- Condition expressions reference fields in the execution context (e.g., `exists(context.confluence.pageId)`).
- Support AND/OR groups and nested conditions through dedicated condition nodes.
- Allow multiple outgoing edges from a node to run in parallel and introduce explicit join nodes to synchronize before downstream steps.

## Airflow Considerations
- Airflow provides mature DAG tooling but introduces a Python dependency and overlaps with the planned .NET workflow engine.
- Recommended: build the orchestration inside DataWorkflows first to keep a unified stack, then reassess Airflow integration if advanced scheduling/monitoring is required beyond the in-house engine.

## Open Questions
- Confirm how connectors will expose "lookup" endpoints to support idempotency checks efficiently.
- Decide on expression syntax/parser for conditions (custom DSL vs. existing library).
- Determine retention policy for workflow execution history and how to surface it in future UI.

## Action & Context Contract Proposal

### Action Interface
```csharp
public interface IWorkflowAction
{
    string ActionType { get; }
    ValueTask<ActionExecutionResult> ExecuteAsync(ActionExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record ActionExecutionRequest(
    WorkflowContext Context,
    IReadOnlyDictionary<string, object?> Parameters,
    WorkflowActionMetadata Metadata);

public sealed record ActionExecutionResult(
    ActionExecutionStatus Status,
    IReadOnlyDictionary<string, object?> Outputs,
    IReadOnlyCollection<WorkflowResourceLink> ResourceLinks,
    IReadOnlyCollection<WorkflowEvent> Events);
```

- `ActionType` aligns with configuration descriptors so the orchestrator can resolve the correct implementation.
- `Parameters` carries the configuration payload (already resolved for this workflow node).
- `Metadata` exposes ids (workflow id/version, node id, request id) and timing hints without polluting the shared context.
- `Outputs` merges back into the shared `WorkflowContext` when status is success or partial success.
- `ResourceLinks` describes external entities (e.g., Confluence page) for persistence in `workflow_link` tables and connector idempotency.
- `Events` are structured log entries/telemetry the engine can emit.

### Workflow Context
```csharp
public sealed class WorkflowContext
{
    public WorkflowMetadata Metadata { get; }
    public WorkflowTrigger Trigger { get; }
    public WorkflowDataBag Data { get; }
    public WorkflowResourceRegistry Resources { get; }

    public WorkflowContextSnapshot CreateSnapshot();
}

public sealed class WorkflowDataBag
{
    private readonly ConcurrentDictionary<string, object?> _values;

    public T? Get<T>(string key);
    public void Set<T>(string key, T? value);
    public IReadOnlyDictionary<string, object?> AsReadOnly();
}

public sealed class WorkflowResourceRegistry
{
    private readonly ConcurrentDictionary<ResourceKey, WorkflowResourceLink> _resources;

    public bool TryGet(ResourceKey key, out WorkflowResourceLink link);
    public void Upsert(WorkflowResourceLink link);
    public IReadOnlyCollection<WorkflowResourceLink> All();
}
```

- `Metadata`: static info (workflow id/version, request id, correlation id, run attempt).
- `Trigger`: normalized payload sent by Slack/UI (e.g., Monday item id, actor info).
- `Data`: normalized key-value store for intra-workflow data (e.g., board summary, column map). Keys follow a namespaced convention (`monday.item`, `slack.channel`).
- `Resources`: canonical index of external artifacts keyed by system + resource type + logical id (e.g., `("monday", "item", "12345")`).
- `CreateSnapshot()` provides an immutable view for logging or storing with the execution record.

### Status & Events
```csharp
public enum ActionExecutionStatus
{
    Succeeded,
    Skipped,
    Failed,
    RetriableFailure
}

public sealed record WorkflowEvent(
    string EventType,
    string Message,
    IReadOnlyDictionary<string, object?> Properties);
```

- `RetriableFailure` allows the orchestrator to apply Polly retry policies at the engine layer.
- `Skipped` supports conditional nodes that decide not to run the action body.

### Open Decisions
- Should `WorkflowDataBag` enforce schemas per provider (e.g., typed records for Monday context) or remain a generic dictionary with conventions?
- How far do we take immutability? (Current sketch uses thread-safe mutators so parallel actions can update shared state; alternative is to treat outputs as deltas that the orchestrator merges serially.)
- What shape should `Parameters` take? Keep as dictionary/`JsonElement` and let actions bind to their own typed record, or introduce a generic `ActionExecutionRequest<TParameters>`?
- How do we version `ActionType` identifiers to allow breaking changes (e.g., `monday.ensure-links/v1`)?
- Where does validation live? (Option: register `IActionParameterValidator` alongside each action.)

### Immediate Follow-Ups
1. Review contract semantics (mutability, typed parameters) and adjust before schema work.
2. Define naming conventions for `WorkflowDataBag` keys and `ResourceKey` composition.
3. Decide on telemetry payload captured in `WorkflowEvent` vs. database columns.
### 2025-10-03 Decisions & Notes
- Keep `WorkflowDataBag` as a generic namespaced dictionary for now; revisit typing if maintenance pain shows up.
- Retain thread-safe mutators on `WorkflowContext` to support concurrent nodes without orchestrator merge overhead.
- `Parameters` handling remains open; evaluating dictionary/JsonElement vs. typed contracts (see summary in conversation).
- Explore lightweight `ActionType` versioning only if breaking changes emerge; default to stable identifiers for foreseeable MVP.
- Parameter validation likely lives with each action (or its owning connector package) via dedicated validators registered alongside the action implementation.
### Gemini Feedback Alignment (2025-10-03)
- Adopt connector-level lookup endpoints (e.g., `GET /api/v1/items?externalReferenceId=`) to support idempotency before create operations.
- Use an embedded expression engine (candidate: Jint for JavaScript expressions) to evaluate workflow conditions instead of building a custom DSL.
- Target a 90-day retention policy with soft-delete flags and scheduled purging for workflow execution history.
- Implement action-level parameter binding helpers so each action converts the raw parameter dictionary into typed records and validates inputs on entry.
- Continue deferring `ActionType` versioning until a breaking change is needed; use suffixes like `/v2` if required later.
### Clarifications (2025-10-03)
- Expression engine: standardize on Jint; authoring conditions as JavaScript expressions is acceptable for MVP.
- Connector lookups: implement GET endpoints with `externalReferenceId` first; evaluate POST/complex payloads later if necessary.
- Retention job: mark as future work--design it, but no immediate implementation.
- Parameter binding helper: proceed with shared binder extension to convert dictionaries into typed action parameter records.
## Condition & Filter Strategy (Draft)

### Workflow Condition Evaluation
- Embed [Jint](https://github.com/sebastienros/jint) inside the engine to execute JavaScript expressions defined in workflow configuration.
- Expose a minimal script context: `context` (read-only snapshot of `WorkflowDataBag`/resources) and `trigger` (request info). No `require`, `eval`, or host accessors.
- Pre-compile expressions per workflow version and cache bytecode to avoid JIT overhead at runtime.
- Require each expression to return a boolean; non-boolean results flag configuration errors during validation.
- Provide helper functions (e.g., `exists(path)`, `coalesce(a,b)`) injected into the script engine to keep config expressions readable.

### Monday Filter Definition Options
1. **Structured JSON AST (Recommended)**
   - Model filters as nested groups: `{ "all": [ ... ], "any": [ ... ], "not": { ... } }` with leaf rules `{ "column": "status", "operator": "eq", "value": "Done" }`.
   - Map directly to Monday GraphQL `query_params.rules` where possible; fallback to in-memory filtering if an operator isnt supported server-side.
   - Pros: deterministic schema (JSON Schema friendly), easy to extend with new operators, safe for translation.
   - Cons: Slightly more verbose config, requires translation layer to Monday API constructs.

2. **Embedded Expressions (Jint) for Filters**
   - Reuse the JavaScript engine to express item predicates (`"item.status === 'Done' && item.timeline.end < cutoff"`).
   - Pros: Extremely flexible, matches condition evaluation model.
   - Cons: Hard to translate to Monday GraphQL; likely forces client-side filtering (more data transfer), weaker static validation.

3. **Custom DSL / SQL-like Syntax**
   - Define a bespoke grammar parsed into an AST.
   - Pros: Could map cleanly to Monday 
 rules
 and be human friendly.
   - Cons: Significant build/maintenance cost; little payoff versus structured JSON.

**Recommendation:** Start with Option 1 (Structured JSON AST). Provide an engine-side evaluator that can both convert to Monday `query_params` and, when not supported, evaluate in-memory using the same AST. Allow advanced scenarios later by adding operator metadata (e.g., `supportsServerSide: false`).

### Filter Schema Kickoff
- Define `MondayFilterDefinition` with properties:
  - `groups`: recursive structure capturing `all`/`any`/`not`.
  - `rules`: leaf comparisons with operators (`eq`, `neq`, `contains`, `before`, `after`, `between`, `isEmpty`, etc.).
  - `subItems`: optional child filter for sub-item evaluation.
- Attach metadata for date handling (`timezone`, `dateFormat`) to ensure consistent comparisons with Monday timeline column values.
- Provide builder utilities so workflow authors can compose filters programmatically if needed.

### Validation & Tooling
- Extend the workflow config JSON Schema to validate filter structure and operator lists.
- During configuration validation, attempt to translate filters into Monday GraphQL; surface warnings when a rule requires client-side filtering.
- Unit-test the translator with real queries (draw from `tests/DataWorkflows.Connector.Monday.IntegrationTests/RealApiIntegrationTests.cs`).

## Monday Filter Schema Iteration

### Step 1: Minimal Schema (Single Rule, No Groups)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://dataworkflows.dev/schemas/monday-filter.schema.json",
  "title": "MondayFilter",
  "type": "object",
  "properties": {
    "rules": {
      "type": "array",
      "items": { "$ref": "#/definitions/rule" },
      "minItems": 1
    }
  },
  "required": ["rules"],
  "additionalProperties": false,
  "definitions": {
    "rule": {
      "type": "object",
      "properties": {
        "column": { "type": "string" },
        "operator": {
          "type": "string",
          "enum": ["eq", "neq", "contains", "isEmpty"]
        },
        "value": {}
      },
      "required": ["column", "operator"],
      "additionalProperties": false,
      "allOf": [
        {
          "if": { "properties": { "operator": { "const": "isEmpty" } } },
          "then": { "not": { "required": ["value"] } }
        },
        {
          "if": { "properties": { "operator": { "not": { "const": "isEmpty" } } } },
          "then": { "required": ["value"] }
        }
      ]
    }
  }
}
```
- Covers basic equality/contains checks on columns; `rules` combined with implicit AND.
- Foundation for more complex compositions.

### Step 2: Introduce Groups (All/Any/Not)
- Extend schema with `group` definition:
```json
"oneOf": [
  { "$ref": "#/definitions/group" },
  {
    "type": "object",
    "properties": { "rules": { "$ref": "#/definitions/rulesArray" } },
    "required": ["rules"],
    "additionalProperties": false
  }
]
```
- `group` structure: `{ "all": [...], "any": [...], "not": { ... } }` with recursive references to `conditionNode` (rule or group).
- Add `rulesArray` definition with minItems validation, optional `label` for debugging.

### Step 3: Time/Range Operators & Metadata
- Expand operator enum (`before`, `after`, `between`, `in` for multi-value).
- Introduce `valueType` hints (`text`, `number`, `date`, `status`) to aid translation and validation.
- Add optional `timezone` on root and `columnId` override for when config uses titles but Monday API needs IDs.

### Step 4: Sub-Item Filters
- Add `subItems` property allowing nested filter definitions to apply to sub-item collections.
- Schema ensures `subItems` is a `conditionNode`; orchestrator decides whether to require `any` matches or `all` per action configuration.

### Validation Phases
1. **Schema validation**: ensure config structure is legal.
2. **Semantic validation**: ensure columns referenced exist (using board metadata), operators compatible with column type, etc.
3. **Translation validation**: test conversion to Monday GraphQL; flag unsupported combinations before runtime.

### Monday GraphQL Translation Strategy
- Mondays `items_page` query supports `query_params` with `rules` (AND) and `or` arrays. Map our schema to those constructs:
  - `group.all` -> `rules` list (AND semantics).
  - `group.any` -> nested object `{ "or": [ ... ] }`.
  - `group.not` -> wrap target in `{ "operator": "not", "rules": [...] }` (if supported) or client-side fallback.
- Leaf rule mapping:
  - `eq` -> `{ "column_id": <id>, "compare_value": <value>, "operator": "equals" }`
  - `neq` -> `"operator": "not_equals"`
  - `contains` -> `"operator": "contains_text"`
  - `before`/`after` -> timeline operators (`"operator": "date_before"`, etc.) with ISO timestamps.
  - `between` -> two rules combined via AND in Monday (until GraphQL supports range natively). Cache conversions.
- Maintain a translation capability matrix with `supportsServerSide` flag per operator/column type. For unsupported combos, mark rule for client-side evaluation.
- Build translator pipeline:
  1. Resolve column metadata (ID, type) using board schema.
  2. Walk filter AST; produce GraphQL `query_params` structure plus `fallbackRules` list requiring client evaluation.
  3. Serialize GraphQL payload and inject into `BuildGetBoardItemsQuery` template.
- For sub-item filters, Monday API lacks direct filtering; run parent query then apply sub-item filters client-side.
- Provide diagnostics output (e.g., log warnings when falling back to client filtering).

### In-Memory Evaluator Plan
- Reuse the same filter AST to evaluate items client-side when GraphQL translation is insufficient.
- Implement evaluator with expression trees or compiled delegates:
  - Convert each leaf rule into a predicate `Func<MondayItemDto, bool>`.
  - Combine predicates for `all` (AND), `any` (OR), `not` (logical NOT).
- Provide operator handlers per column type:
  - Text comparisons: use culture-insensitive equality/contains.
  - Numeric comparisons: parse numbers safely; handle nulls.
  - Date/timeline comparisons: parse Monday timeline column JSON into strongly-typed range structure; compare with DateTimeOffset.
  - Status/Dropdown: compare against label or index depending on metadata.
- Cache compiled predicates per workflow version to avoid re-building for every item.
- Support sub-item evaluation by running evaluator against sub-item collection and applying aggregator semantics (e.g., any sub-item matches).
- Integration testing plan: feed real board data (from integration tests) through evaluator and compare results to manual expectations.

### Monday Filter Implementation Roadmap (2025-10-03)
- Introduce `IMondayFilterTranslator` in connector application layer returning:
  - `MondayGraphQlQueryParams` for server-side filtering (`query_params`).
  - `Func<MondayItemDto, bool>?` in-memory predicate for fallback rules.
- Maintain a configurable capability matrix mapping `(columnType, operator)` to server-side support; store in configuration to enable updates without redeploy.
- Implementation sequence:
  1. Add minimal JSON Schema + C# record (`MondayFilterDefinition`) supporting simple AND rules.
  2. Build translator implementation for Step 1 schema (AND rules only).
  3. Implement in-memory evaluator shared by translator result for fallback execution.
  4. Integrate translator/evaluator into `MondayApiClient` (replacing `GetItemsFilterModel`).
  5. Iterate schema/translator to add groups (`all/any/not`), range operators, metadata, and sub-item filters.
- Update workflow config JSON Schema and validation pipeline to reference the new filter schema once stabilized.
### Implementation Snapshot (2025-10-03)
- Minimal Monday filter schema (`Resources/Schemas/monday-filter.schema.json`) and typed definition (`MondayFilterDefinition`) now committed.
- `IMondayFilterTranslator` resolves config into Monday GraphQL `query_params` and client predicates; fallback evaluator handles equality/contains/isEmpty with created-at range checks.
- `MondayApiClient` integrates the translator, builds GraphQL arguments, and applies predicates for sub-item filtering; controllers/tests updated to use `MondayFilterDefinition`.
- Added JSON schema + docs foundations to extend toward grouped conditions in later iterations.
### Update (2025-10-03)
- Step 2 implemented for Monday filters: added grouped condition support (`all`/`any`/`not`) in schema and translator. Server translation remains for pure AND chains; OR/NOT conditions fall back to the in-memory evaluator.
- Translator unit tests cover grouped scenarios and ensure predicate fidelity.
- Integration coverage now uses typed `MondayFilterDefinition` fixtures (`tests/DataWorkflows.Connector.Monday.IntegrationTests/MondayFilterDefinitionFixtures.cs`) instead of JSON; ensures the workflow engine remains responsible for configuration translation.
- Step 3 completed (2025-10-10): expanded Monday filter operators (timeline/range comparisons, numeric/date metadata) with schema, translator, evaluator, and tests updated accordingly.
- Step 4 completed (2025-10-14): sub-item filters wired through `MondaySubItemFilter`, translator now emits nested predicates, `MondayApiClient` materializes sub-items to apply aggregation, and unit/integration coverage exercises representative boards.
- Step 5 completed (2025-10-15): update and activity-log filters now compile into predicates, `MondayApiClient` evaluates them alongside sub-item logic, and unit/integration coverage targets representative boards.

## Implementation Status (2025-10-05)

### Completed: Server-Side Translation & Guardrails

**Server-side translation strategy implemented:**
- Simple AND-only filter chains now translate to Monday GraphQL `query_params`
- Supported operators for server-side: `eq` (any_of), `neq` (not_any_of), `contains` (contains_text), `isEmpty` (is_empty)
- Filters with OR/NOT groups or unsupported operators (date ranges, numeric comparisons) fall back to client-side evaluation
- Nested ALL groups fully supported for server-side translation
- Verified working against live Monday API

**Guardrails system operational:**
- `IMondayFilterGuardrailValidator` validates filter complexity before translation
- Configurable limits via `appsettings.json`:
  - `MaxDepth`: maximum nesting level (default: 3)
  - `MaxTotalRuleCount`: maximum rules across all dimensions (default: 50)
  - `ComplexityWarningThreshold`: soft limit for warnings (default: 30)
- `GuardrailViolationException` thrown when hard limits exceeded (returns HTTP 400)
- Warnings logged when complexity threshold breached but not blocking
- Guardrails can be disabled via configuration for advanced use cases
- Exception handling verified against live Monday API

**Testing coverage:**
- **Unit tests (41 passing):**
  - 10 guardrail validator tests covering all limit types, sub-item/update/activity rule counting, nested depth
  - 11 server-side translation tests covering supported operators, AND-only detection, fallback scenarios
  - 20 existing translator tests updated for server-side behavior

- **Integration tests (5 new, all passing):**
  - `ServerSideTranslation_ShouldExecuteSimpleAndChains`: Verified server-side query params execute correctly against Monday API
  - `MaxComplexityFilter_ShouldExecuteSuccessfully`: Filter at exactly maximum complexity (depth=3, rules=50) executes without errors
  - `ExceedMaxDepth_ShouldThrowGuardrailException`: Filter exceeding depth limit correctly throws `GuardrailViolationException`
  - `ExceedMaxRuleCount_ShouldThrowGuardrailException`: Filter exceeding rule count (51 rules) correctly throws exception
  - `DisabledGuardrails_ShouldAllowComplexFilters`: Deeply nested filters (depth=10) execute when guardrails disabled

**Real-world validation:**
- Tests executed against live Monday board (ID: 18094128211)
- Server-side translation reduces network overhead by pushing filters to GraphQL layer
- Guardrail violations caught before API execution, preventing expensive client-side operations
- Maximum complexity filters process successfully within timeout limits

**Architecture notes:**
- Followed SOLID principles: Single Responsibility (validator separate from translator), Dependency Inversion (interfaces), Open/Closed (extensible via configuration)
- Clean Architecture: Domain exceptions, Application layer services, Infrastructure integration
- Translator validates via injected `IMondayFilterGuardrailValidator` before proceeding
- Configuration injected via `IOptions<GuardrailOptions>` pattern

## Next Steps Snapshot
- **Server-side translation expansion**: Monitor real-world usage to identify additional operators worth translating (e.g., date comparisons if Monday API supports them)
- **Caching/pre-computed lookups**: Deferred until performance metrics prove necessity (activity logs, updates)
- **Metrics analysis**: Use captured complexity metrics to tune guardrail thresholds based on actual board sizes and query performance
- **Performance benchmarking**: Compare server-side vs client-side filtering performance on large boards (>1000 items)






