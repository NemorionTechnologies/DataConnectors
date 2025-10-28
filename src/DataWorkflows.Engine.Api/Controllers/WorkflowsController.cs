using Microsoft.AspNetCore.Mvc;
using DataWorkflows.Engine.Core.Domain.Models;
using DataWorkflows.Engine.Core.Domain.Parsing;
using DataWorkflows.Engine.Core.Domain.Validation;
using DataWorkflows.Engine.Core.Application.Registry;
using DataWorkflows.Engine.Core.Application.Evaluation;
using DataWorkflows.Data.Repositories;

namespace DataWorkflows.Engine.Api.Controllers;

[ApiController]
[Route("api/v1/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly WorkflowParser _parser;
    private readonly GraphValidator _graphValidator;
    private readonly ActionRegistry _actionRegistry;
    private readonly JintConditionEvaluator _conditionEvaluator;

    public WorkflowsController(
        IConfiguration config,
        WorkflowParser parser,
        GraphValidator graphValidator,
        ActionRegistry actionRegistry,
        JintConditionEvaluator conditionEvaluator)
    {
        _config = config;
        _parser = parser;
        _graphValidator = graphValidator;
        _actionRegistry = actionRegistry;
        _conditionEvaluator = conditionEvaluator;
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkflows([FromQuery] string? status = null, [FromQuery] bool? isEnabled = null)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var repo = new WorkflowRepository(connectionString);
        var workflows = await repo.GetAllAsync(status, isEnabled);
        return Ok(workflows);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkflow(string id, [FromQuery] int? version = null)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);
        var defRepo = new WorkflowDefinitionRepository(connectionString);

        var workflow = await workflowRepo.GetByIdAsync(id);
        if (workflow == null)
            return NotFound(new { error = $"Workflow not found: {id}" });

        WorkflowDefinitionRecord? definition;

        if (version.HasValue)
        {
            definition = await defRepo.GetByIdAndVersionAsync(id, version.Value);
        }
        else if (workflow.CurrentVersion.HasValue)
        {
            definition = await defRepo.GetByIdAndVersionAsync(id, workflow.CurrentVersion.Value);
        }
        else
        {
            // Draft workflow - get version 0
            definition = await defRepo.GetDraftVersionAsync(id);
        }

        return Ok(new
        {
            id = workflow.Id,
            displayName = workflow.DisplayName,
            description = workflow.Description,
            currentVersion = workflow.CurrentVersion,
            status = workflow.Status,
            isEnabled = workflow.IsEnabled,
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt,
            definition = definition != null ? _parser.Parse(definition.DefinitionJson) : null
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);
        var defRepo = new WorkflowDefinitionRepository(connectionString);

        // Validate the workflow definition can be parsed
        WorkflowDefinition workflowDef;
        try
        {
            workflowDef = _parser.Parse(request.DefinitionJson);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Invalid workflow JSON", details = ex.Message });
        }

        // Validate basic structure (graph validation)
        try
        {
            _graphValidator.Validate(workflowDef);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Workflow validation failed", details = ex.Message });
        }

        var workflowId = workflowDef.Id;

        // Check if workflow exists
        var existing = await workflowRepo.GetByIdAsync(workflowId);

        if (existing == null)
        {
            // Create new Draft workflow
            await workflowRepo.CreateDraftAsync(workflowId, workflowDef.DisplayName, request.Description);
            await defRepo.CreateOrUpdateDraftAsync(workflowId, request.DefinitionJson);

            return Created($"/api/v1/workflows/{workflowId}", new
            {
                workflowId,
                status = "Draft",
                version = 0,
                message = "Draft workflow created"
            });
        }
        else if (existing.Status == "Draft")
        {
            // Update existing Draft
            await workflowRepo.UpdateDraftAsync(workflowId, workflowDef.DisplayName, request.Description);
            await defRepo.CreateOrUpdateDraftAsync(workflowId, request.DefinitionJson);

            return Ok(new
            {
                workflowId,
                status = "Draft",
                version = 0,
                message = "Draft workflow updated"
            });
        }
        else
        {
            // Cannot update Active/Archived workflows
            return BadRequest(new
            {
                error = $"Cannot update workflow in {existing.Status} status",
                details = "Only Draft workflows can be modified. Use /publish endpoint to create new versions."
            });
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishWorkflow(string id, [FromQuery] bool autoActivate = true)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);
        var defRepo = new WorkflowDefinitionRepository(connectionString);

        // Get workflow metadata
        var workflow = await workflowRepo.GetByIdAsync(id);
        if (workflow == null)
            return NotFound(new { error = $"Workflow not found: {id}" });

        // Get draft definition (version 0)
        var draftDef = await defRepo.GetDraftVersionAsync(id);
        if (draftDef == null)
            return BadRequest(new { error = "No draft definition found to publish" });

        // Parse and validate
        WorkflowDefinition workflowDef;
        try
        {
            workflowDef = _parser.Parse(draftDef.DefinitionJson);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Invalid workflow JSON", details = ex.Message });
        }

        // Full publish-time validation
        var validator = new WorkflowValidator(_graphValidator, _actionRegistry, _conditionEvaluator);
        var validationResult = validator.Validate(workflowDef);

        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                error = "Workflow validation failed",
                errors = validationResult.Errors,
                warnings = validationResult.Warnings
            });
        }

        // Publish the version
        var (newVersion, created) = await defRepo.PublishVersionAsync(id, draftDef.DefinitionJson);

        // Update workflow metadata
        await workflowRepo.PublishAsync(id, newVersion, autoActivate);

        var status = autoActivate ? "Active" : "Draft";

        return Ok(new
        {
            workflowId = id,
            version = newVersion,
            status,
            created,
            message = created
                ? $"Published as version {newVersion} ({status})"
                : $"Existing version {newVersion} reused (idempotent)",
            warnings = validationResult.Warnings
        });
    }

    [HttpPost("{id}/archive")]
    public async Task<IActionResult> ArchiveWorkflow(string id)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);

        try
        {
            await workflowRepo.ArchiveAsync(id);
            return Ok(new { workflowId = id, status = "Archived", message = "Workflow archived successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/reactivate")]
    public async Task<IActionResult> ReactivateWorkflow(string id)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);

        try
        {
            await workflowRepo.ReactivateAsync(id);
            return Ok(new { workflowId = id, status = "Active", message = "Workflow reactivated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/validate")]
    public async Task<IActionResult> ValidateWorkflow(string id, [FromQuery] int? version = null)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);
        var defRepo = new WorkflowDefinitionRepository(connectionString);

        var workflow = await workflowRepo.GetByIdAsync(id);
        if (workflow == null)
            return NotFound(new { error = $"Workflow not found: {id}" });

        WorkflowDefinitionRecord? definition;

        if (version.HasValue)
        {
            definition = await defRepo.GetByIdAndVersionAsync(id, version.Value);
        }
        else if (workflow.CurrentVersion.HasValue)
        {
            definition = await defRepo.GetByIdAndVersionAsync(id, workflow.CurrentVersion.Value);
        }
        else
        {
            // Draft workflow - get version 0
            definition = await defRepo.GetDraftVersionAsync(id);
        }

        if (definition == null)
            return NotFound(new { error = $"Workflow definition not found for version {version}" });

        // Parse workflow
        WorkflowDefinition workflowDef;
        try
        {
            workflowDef = _parser.Parse(definition.DefinitionJson);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Invalid workflow JSON", details = ex.Message });
        }

        // Validate
        var validator = new WorkflowValidator(_graphValidator, _actionRegistry, _conditionEvaluator);
        var validationResult = validator.Validate(workflowDef);

        return Ok(new
        {
            workflowId = id,
            version = definition.Version,
            isValid = validationResult.IsValid,
            errors = validationResult.Errors,
            warnings = validationResult.Warnings
        });
    }

    [HttpPost("validate")]
    public IActionResult ValidateWorkflowDefinition([FromBody] ValidateWorkflowRequest request)
    {
        // Parse workflow
        WorkflowDefinition workflowDef;
        try
        {
            workflowDef = _parser.Parse(request.DefinitionJson);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Invalid workflow JSON", details = ex.Message });
        }

        // Validate
        var validator = new WorkflowValidator(_graphValidator, _actionRegistry, _conditionEvaluator);
        var validationResult = validator.Validate(workflowDef);

        return Ok(new
        {
            workflowId = workflowDef.Id,
            isValid = validationResult.IsValid,
            errors = validationResult.Errors,
            warnings = validationResult.Warnings
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkflow(string id)
    {
        var connectionString = _config.GetConnectionString("Postgres")!;
        var workflowRepo = new WorkflowRepository(connectionString);
        var defRepo = new WorkflowDefinitionRepository(connectionString);

        try
        {
            await defRepo.DeleteDraftAsync(id);
            await workflowRepo.DeleteDraftAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record CreateWorkflowRequest(
    string DefinitionJson,
    string? Description = null
);

public record ValidateWorkflowRequest(
    string DefinitionJson
);
