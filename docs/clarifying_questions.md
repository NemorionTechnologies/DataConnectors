Clarifying Questions and Answers:
  
Q: When/how do actions get registered in the ActionCatalog table?
A: On startup, each connector should attempt to call the configured API endpoint of the admin API on the engine (or eventually orchestrator):
  `POST /api/v1/admin/actions/register` or `PUT /api/v1/admin/actions/register` with an idempotent upsert
  Sample body:
  ```
  {
  "connectorId": "monday",
  "actions": [
    {
      "actionType": "monday.get-items",
      "displayName": "Get Board Items",
      "description": "Retrieve all items from a board",
      "parameterSchema": { ...json schema... },
      "outputSchema": { ...json schema... },
      "requiresAuth": true,
      "isEnabled": true
    },
    {
      "actionType": "monday.update-item",
      "displayName": "Update Board Item",
      "description": "Patch fields on a Monday item",
      "parameterSchema": { ... },
      "outputSchema": { ... },
      "requiresAuth": true,
      "isEnabled": true
    }
  ]
}
```

Q: What format should we use for parameter/output schemas?
A: JSON:
- ParameterSchema is JSON Schema describing what that action expects in its parameters.
- OutputSchema is JSON Schema describing what that action will write to context.data[nodeId].

Q: Based on the spec, does this match what you want? Any additional columns needed?
  CREATE TABLE ActionCatalog (
    Id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ActionType        TEXT NOT NULL UNIQUE,  -- e.g. "monday.get-items"
    ConnectorId       TEXT NOT NULL,          -- e.g. "monday"
    DisplayName       TEXT NOT NULL,
    Description       TEXT,
    ParameterSchema   JSONB NOT NULL,         -- JSON Schema
    OutputSchema      JSONB NOT NULL,         -- JSON Schema
    IsEnabled         BOOLEAN DEFAULT TRUE,
    RequiresAuth      BOOLEAN DEFAULT TRUE,
    CreatedAt         TIMESTAMPTZ DEFAULT NOW(),
    UpdatedAt         TIMESTAMPTZ DEFAULT NOW()
  );  
A: Keep a surrogate UUID and make (ConnectorId, ActionType) unique so we can version/soft-delete actions in the future.

Q: Which actions from the Monday integration tests should we implement?  Which ones are tested and should be in scope for Bundle 5?
A:   We can start with get-items for the first implementation.  Test multiple levels of complexity with increasing depth of filters and logic. See tests\DataWorkflows.Connector.Monday.IntegrationTests\MondayFilterDefinitionFixtures.cs for examples.
Required for Bundle 5 to complete will be:
  get-board
  get-items  
  get-updates
  get-actionhistory
  get-subitems
  create-item
  create-subitem
  update-column (variations on this will include updating a timeline column updating a status column, updating a link column, updating a text/comment column)
  update-subitem-column (same variations as update-column)

Q: How should the Engine's ActionRegistry get populated?
A: Cache in-memory, refresh periodically, build in an endpoint to flush cache on request

Q: What should happen when validation fails?
A: Assumptions correct

Q: Are connectors:
A: Separate services (Monday connector runs as separate HTTP service, engine calls it)

Q: For existing Monday connector that's already running:
A: The Monday connector should push its catalog to the engine on startup. We’ll expose an internal admin endpoint on the engine for registration. The connector should not talk to the DB directly. The connector should not rely on the engine polling it. The endpoint URL should come from config/env so we can point the same connector container at different engine deployments. The payload includes action types + JSON Schemas; the engine stores those in ActionCatalog and uses them for validation and runtime.

Q: For unit tests that mock the ActionCatalog, what's your preference for testability?
A: SOLID Principles, program to the interface, mock the interface.

------------------------------------------------------------------------------------
Q: What HTTP endpoint structure should the Monday connector expose for action execution?
A: Single, generic execute endpoint
POST /api/v1/actions/execute
Request:
```
{
  "actionType": "monday.get-items",
  "parameters": { ... },      // already validated params for this node
  "executionContext": {
    "workflowExecutionId": "2f117ff5-...",
    "nodeId": "get-board-items",
    "principal": {
      "userId": "U123",
      "email": "doug@example.com",
      "displayName": "Doug"
    },
    "correlationId": "..."    // for trace/logging
  }
}
```

Respone (happy):
```
{
  "status": "Succeeded",
  "outputs": { ... }          // must match OutputSchema for this action
}
```
Response (recoverable/transient issue):
```
{
  "status": "RetriableFailure",
  "error": "rate limited"
}
```
Response (permanent failure):
```
{
  "status": "Failed",
  "error": "board not found"
}
```


Q: Where exactly does parameter translation from workflow JSON to connector DTOs happen?
A: The connector should accept the same parameters shape the engine validated against (ParameterSchema), and each action implementation inside the connector handles translating those into Monday API calls.

Q: Where should the action implementation classes live within the Monday connector project?
A: We already defined the runtime execution interface in the engine as:
```
public interface IWorkflowAction {
  string Type { get; } // "core.echo"
  Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}
```
And ActionExecutionContext includes WorkflowExecutionId, NodeId, Parameters, and an injected service provider.  Yes, create src/DataWorkflows.Connector.Monday/Actions/.  Each action class implements the same IWorkflowAction interface from DataWorkflows.Contracts.Actions. Each action’s .Type matches the actionType string in ActionCatalog (e.g. "monday.get-items").  Those classes can depend on Monday-specific service abstractions (like IMondayClient) that do the actual REST calls.  So yes: DataWorkflows.Connector.Monday/Actions/ + they implement IWorkflowAction from a shared contracts assembly.


Q: How should we generate/define the JSON Schemas for ParameterSchema and OutputSchema?
A: boring and consistent - Each connector should use C# POCO's and generate JSON Schemas using a library at build or startup.  We've already discussed registering them with the engine/orchestrator.

Q: At what points should the engine validate action parameters against schemas?
A: Both at publish AND at execution time.

Draft/Publish validation:
Engine loads workflow JSON and runs the validator.
For each node with actionType:  Look up that actionType in ActionCatalog (must exist and be IsEnabled=true). Validate that node’s declared parameters shape against that action’s ParameterSchema. Report errors so authors know before publish.

Execution-time validation: 
Right before executing a node, after templating is applied (Scriban render → JSON → dictionary), the engine validates the now-concrete params against that same schema.
If validation fails at runtime: The node attempt is recorded with Status=RetriableFailure or Failed, same as any other action error, and we persist that attempt in ActionExecutions.

Q: Should the connector validate again?
A: The connector can assume the engine did its job, but for defense-in-depth it’s fine if the connector also validates its DTO bind using the same schema or model binder.

Q: How should the Monday connector know what engine URL to register with?
A: Environment variable: WORKFLOW_ENGINE_URL.  Yes the connector should retry with backoff if the engine isn't up yet.

Q: What should the standard action execution response format look like?
A: We should mirror the in-process ActionExecutionResult, because the conductor already understands that shape and already persists it (Status, Outputs, ErrorMessage).
```
{
  "status": "Succeeded" | "Failed" | "RetriableFailure" | "Skipped",
  "outputs": { ... },
  "error": "optional string"
}
```
Rules:  outputs must conform to the published OutputSchema for that actionType.  The engine stores outputs as the OutputsJson for that node attempt, and also drops it into the workflow databag (context.data[nodeId]). For non-success statuses, outputs might be {} or omitted; error should explain what went wrong.
We do not wrap in {"success": true, "data": ...}. Use the ActionExecutionResult shape so engine code doesn’t fork between “local action” vs “remote connector.”


Q: What error handling conventions should we follow for action execution failures?
A: The connector should always return 200 OK with the body containing: status: "Succeeded" | "RetriableFailure" | "Failed" | "Skipped"; error: message if not Succeeded.
Use status to communicate:  RetriableFailure: “please retry me” (e.g. 429 from Monday, network blip), Failed: permanent (“board doesn’t exist / validation failed”),  Succeeded: all good
Only use non-200 HTTP codes for cases where the connector itself is broken (deserialization error, connector crashed, etc.). The engine can treat any non-200 as "Failed" unless we later decide to map certain 5xx to "RetriableFailure".


Q: What scope should the integration tests cover?
A: workflow JSON → engine validation → connector registration → action execution → result storage, BUT we should be using a test double server instead of the real data connector for our engine tests.
The test double server should also do a register call to the engine to verify that pathway works as well.

Q: Should they test only the happy path or include error scenarios?
A: ALWAYS test error scenarios extensively!

Q: Which JSON Schema validation library should we use in the engine?
A: NJsonSchema

Q: Should the same library be used in both engine and connectors?
A: Yes

Q: Are there any specific JSON Schema draft versions we should target (draft-07, 2019-09, 2020-12)?
A: 2020-12