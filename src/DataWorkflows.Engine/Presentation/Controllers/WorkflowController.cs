using DataWorkflows.Engine.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Engine.Presentation.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowOrchestrator _orchestrator;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowOrchestrator orchestrator,
        ILogger<WorkflowController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Executes a workflow by ID.
    /// </summary>
    [HttpPost("{workflowId}/execute")]
    public async Task<IActionResult> ExecuteWorkflow(
        string workflowId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received request to execute workflow: {WorkflowId}", workflowId);

        var result = await _orchestrator.ExecuteWorkflowAsync(workflowId, cancellationToken);

        return Ok(new { workflowId, result, timestamp = DateTime.UtcNow });
    }

}
