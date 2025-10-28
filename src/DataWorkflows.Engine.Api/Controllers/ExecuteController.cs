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
using DataWorkflows.Data.Repositories;
using Microsoft.Extensions.Options;

namespace DataWorkflows.Engine.Api.Controllers;

[ApiController]
[Route("api/v1/workflows")]
public class ExecuteController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly WorkflowConductor _conductor;
    private readonly WorkflowParser _parser;
    private readonly WorkflowCatalogOptions _catalogOptions;

    public ExecuteController(
        IConfiguration config,
        WorkflowConductor conductor,
        WorkflowParser parser,
        IOptions<WorkflowCatalogOptions> catalogOptions)
    {
        _config = config;
        _conductor = conductor;
        _parser = parser;
        _catalogOptions = catalogOptions.Value;
    }

    [HttpPost("{workflowId}/execute")]
    public async Task<IActionResult> Execute(
        string workflowId,
        [FromBody] ExecuteRequest request,
        [FromQuery] string? fixture = null,
        [FromQuery] int? version = null,
        [FromServices] IWebHostEnvironment env = null!)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        WorkflowDefinition workflow;

        // If fixture is specified, load from file (for testing/development)
        if (fixture != null)
        {
            var relPath = fixture;
            var fullPath = Path.Combine(env.ContentRootPath, relPath);

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { error = $"Fixture not found: {fixture}" });

            var workflowJson = System.IO.File.ReadAllText(fullPath);
            workflow = _parser.Parse(workflowJson);
        }
        else
        {
            // Load workflow from database
            var workflowRepo = new WorkflowRepository(connectionString);
            var defRepo = new WorkflowDefinitionRepository(connectionString);

            var workflowRecord = await workflowRepo.GetByIdAsync(workflowId);
            if (workflowRecord == null)
                return NotFound(new { error = $"Workflow not found: {workflowId}" });

            // Check workflow status
            if (workflowRecord.Status == "Archived")
                return BadRequest(new { error = "Cannot execute archived workflow" });

            if (workflowRecord.Status == "Draft" && !_catalogOptions.AllowDraftExecution)
                return BadRequest(new { error = "Draft workflow execution is disabled. Publish the workflow first." });

            if (!workflowRecord.IsEnabled)
                return BadRequest(new { error = "Workflow is disabled" });

            // Determine which version to execute
            int versionToExecute;
            if (version.HasValue)
            {
                versionToExecute = version.Value;
            }
            else if (workflowRecord.Status == "Draft")
            {
                versionToExecute = 0; // Draft version
            }
            else if (workflowRecord.CurrentVersion.HasValue)
            {
                versionToExecute = workflowRecord.CurrentVersion.Value;
            }
            else
            {
                return BadRequest(new { error = "No published version available" });
            }

            var definition = await defRepo.GetByIdAndVersionAsync(workflowId, versionToExecute);
            if (definition == null)
                return NotFound(new { error = $"Workflow version not found: {workflowId} v{versionToExecute}" });

            workflow = _parser.Parse(definition.DefinitionJson);
        }

        // Execute workflow
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
