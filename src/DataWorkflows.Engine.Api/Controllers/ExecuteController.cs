using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataWorkflows.Engine.Core.Configuration;
using Microsoft.AspNetCore.Mvc;
using DataWorkflows.Engine.Core.Domain.Models;
using DataWorkflows.Engine.Core.Domain.Parsing;
using DataWorkflows.Engine.Core.Application.Orchestration;
using DataWorkflows.Engine.Core.Application.Registry;
using DataWorkflows.Engine.Core.Application.Templating;

namespace DataWorkflows.Engine.Api.Controllers;

[ApiController]
[Route("api/v1/workflows")]
public class ExecuteController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly WorkflowConductor _conductor;

    public ExecuteController(IConfiguration config, WorkflowConductor conductor)
    {
        _config = config;
        _conductor = conductor;
    }

    [HttpPost("{workflowId}/execute")]
    public async Task<IActionResult> Execute(string workflowId,
    [FromBody] ExecuteRequest request,
    [FromQuery] string? fixture = null,
    [FromServices] IWebHostEnvironment env = null!)
    {
        // Pick a fixture: ?fixture=fixtures/bundle3/fanout-fanin-workflow.json
        // or default per-bundle (keep your existing test id)
        var relPath = fixture ?? "fixtures/bundle1/simple-echo-workflow.json";
        var fullPath = Path.Combine(env.ContentRootPath, relPath);

        var workflowJson = System.IO.File.ReadAllText(fullPath); // <-- replaces hardcoded JSON

        var parser = new WorkflowParser();
        var workflow = parser.Parse(workflowJson);

        var connectionString = _config.GetConnectionString("Postgres")!;
        // Controller only handles HTTP concerns - Conductor owns execution lifecycle
        var result = await _conductor.ExecuteAsync(
            workflow,
            request.Trigger ?? new Dictionary<string, object>(),
            request.Vars ?? new Dictionary<string, object>(),
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
