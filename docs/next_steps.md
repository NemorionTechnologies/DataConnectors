# Next Steps Roadmap

## 1. Solidify Workflow Runtime Contract
- Finalize the action interface (`ExecuteAsync`, context mutation rules, idempotency semantics) and shared execution context schema before locking database tables.
- Pick an expression/DSL strategy for conditional logic and Monday filters (evaluate libraries vs. custom JSON-based syntax) so downstream components align.

## 2. Design Persistence Schema
- Using the runtime contract, draft tables for `workflow`, `workflow_version`, `workflow_node`, `workflow_edge`, `workflow_parameter` plus execution artifacts (`workflow_execution`, `workflow_action_execution`, `workflow_link`).
- Document migration path from file-backed configs to database storage.

## 3. Define Configuration Schema & Validation
- Author the initial JSON Schema covering DAG structure, node parameters, Monday board/column mappings, and conditional expressions.
- Build a validator step in CI to lint configs (keeps Git-based definitions trustworthy until DB-backed).

## 4. Implement Engine Skeleton
- Build a minimal orchestrator that loads configuration, constructs the DAG, and executes nodes sequentially with basic logging.
- Integrate Polly policies at the workflow layer (wrapping action execution) on top of connector-level retries.

## 5. Prototype Workflows Incrementally
1. **Workflow One** – Invoke Monday connector for a configured board, return all items, persist execution metadata.
2. **Workflow Two** – Extend with simple filter support (reuse patterns from `tests/DataWorkflows.Connector.Monday.IntegrationTests/RealApiIntegrationTests.cs`), verifying filtered queries and result shaping.
3. **Workflow Three** – Add conditional branch: inspect a column on a specific item and, when empty, emit an action that writes a link back to Monday.

## 6. Evolve Filtering Capability
- Design filter objects that support nested AND/OR, comparisons on timelines, sub-item criteria, etc. Start with a declarative JSON structure that can expand toward the complex cases seen in integration tests.
- Ensure the Monday connector exposes a consistent API for these filters, including translation to Monday’s GraphQL.
- Quick references for Step 4: definition model in `src/DataWorkflows.Connector.Monday/Application/Filters/MondayFilterDefinition.cs`, translator + evaluator baseline in `src/DataWorkflows.Connector.Monday/Application/Filters/MondayFilterTranslator.cs`, typed fixtures powering integration tests in `tests/DataWorkflows.Connector.Monday.IntegrationTests/MondayFilterDefinitionFixtures.cs`.
- Step 5 target: bring updates/activity log filtering online via `MondayUpdateFilter` and parallel predicates before widening to activity log analytics.

## 7. Telemetry & Observability
- Add structured logging and metrics for each workflow/action execution to support future UI and troubleshooting.
- Plan how to surface execution state via API for Slack responses and the upcoming GUI.

## 8. Reassess External Orchestrators
- Once the in-house engine handles branching and filters, revisit whether integrating Airflow (or alternatives) adds value beyond current needs.

## Reference Notes
- Typed Monday filter fixtures live in `tests/DataWorkflows.Connector.Monday.IntegrationTests/MondayFilterDefinitionFixtures.cs`; `FilterDefinitionIntegrationTests` executes them end-to-end against the live API.

