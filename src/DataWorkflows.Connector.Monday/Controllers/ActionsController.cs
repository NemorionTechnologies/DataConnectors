using DataWorkflows.Contracts.Actions;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.Monday.Controllers;

/// <summary>
/// Generic action execution endpoint for the Monday connector.
/// The workflow engine calls this endpoint to execute Monday actions.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class ActionsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActionsController> _logger;

    public ActionsController(
        IServiceProvider serviceProvider,
        ILogger<ActionsController> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute a workflow action.
    /// This is the generic entry point called by the workflow engine for all Monday actions.
    /// </summary>
    /// <param name="request">The action execution request from the workflow engine.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action execution result with status, outputs, and optional error message.</returns>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body cannot be null." });

        if (string.IsNullOrWhiteSpace(request.ActionType))
            return BadRequest(new { error = "ActionType is required." });

        try
        {
            _logger.LogInformation(
                "Executing action {ActionType} for workflow {WorkflowExecutionId}, node {NodeId}",
                request.ActionType,
                request.ExecutionContext?.WorkflowExecutionId,
                request.ExecutionContext?.NodeId);

            // Find the action implementation by type
            var action = GetActionByType(request.ActionType);

            if (action == null)
            {
                _logger.LogWarning("Action type {ActionType} not found", request.ActionType);
                return new JsonResult(new ActionExecutionResultDto
                {
                    Status = "Failed",
                    Outputs = new Dictionary<string, object?>(),
                    Error = $"Action type '{request.ActionType}' is not registered in this connector."
                });
            }

            // Create execution context
            var context = new ActionExecutionContext(
                WorkflowExecutionId: request.ExecutionContext?.WorkflowExecutionId ?? Guid.Empty,
                NodeId: request.ExecutionContext?.NodeId ?? "unknown",
                Parameters: request.Parameters ?? new Dictionary<string, object?>(),
                Services: _serviceProvider);

            // Execute the action
            var result = await action.ExecuteAsync(context, cancellationToken);

            // Map to DTO and return
            var resultDto = new ActionExecutionResultDto
            {
                Status = result.Status.ToString(),
                Outputs = result.Outputs,
                Error = result.ErrorMessage
            };

            _logger.LogInformation(
                "Action {ActionType} completed with status {Status} for node {NodeId}",
                request.ActionType,
                result.Status,
                request.ExecutionContext?.NodeId);

            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error executing action {ActionType}", request.ActionType);

            // Return 200 OK with Failed status (as per spec)
            return Ok(new ActionExecutionResultDto
            {
                Status = "Failed",
                Outputs = new Dictionary<string, object?>(),
                Error = $"Connector error: {ex.Message}"
            });
        }
    }

    private IWorkflowAction? GetActionByType(string actionType)
    {
        // Get all registered IWorkflowAction implementations
        var actions = _serviceProvider.GetServices<IWorkflowAction>();

        // Find the action matching the requested type
        return actions.FirstOrDefault(a => a.Type.Equals(actionType, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Request model for action execution.
/// This matches the format defined in the clarifying questions document.
/// </summary>
public sealed record ActionExecutionRequest
{
    public required string ActionType { get; init; }
    public Dictionary<string, object?>? Parameters { get; init; }
    public ExecutionContextDto? ExecutionContext { get; init; }
}

/// <summary>
/// Execution context information from the workflow engine.
/// </summary>
public sealed record ExecutionContextDto
{
    public Guid WorkflowExecutionId { get; init; }
    public string? NodeId { get; init; }
    public PrincipalDto? Principal { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Principal (user) information.
/// </summary>
public sealed record PrincipalDto
{
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>
/// Action execution result DTO.
/// This matches the ActionExecutionResult format but uses string for Status.
/// </summary>
public sealed record ActionExecutionResultDto
{
    public required string Status { get; init; }  // "Succeeded", "Failed", "RetriableFailure", "Skipped"
    public required Dictionary<string, object?> Outputs { get; init; }
    public string? Error { get; init; }
}
