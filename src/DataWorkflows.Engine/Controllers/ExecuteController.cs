using Microsoft.AspNetCore.Mvc;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Parsing;
using DataWorkflows.Engine.Orchestration;
using DataWorkflows.Engine.Registry;

namespace DataWorkflows.Engine.Controllers;

[ApiController]
[Route("api/v1/workflows")]
public class ExecuteController : ControllerBase
{
    private readonly IConfiguration _config;

    public ExecuteController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("{workflowId}/execute")]
    public async Task<IActionResult> Execute(string workflowId, [FromBody] ExecuteRequest request)
    {
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

        var connectionString = _config.GetConnectionString("Postgres")!;
        var conductor = new WorkflowConductor(new ActionRegistry());

        // Controller only handles HTTP concerns - Conductor owns execution lifecycle
        var result = await conductor.ExecuteAsync(
            workflow,
            request.Trigger ?? new(),
            requestId: Guid.NewGuid().ToString(),
            connectionString
        );

        return Accepted(new
        {
            executionId = result.ExecutionId,
            status = result.Status,
            statusUrl = $"/api/v1/executions/{result.ExecutionId}"
        });
    }
}
