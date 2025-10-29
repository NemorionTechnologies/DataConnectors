using DataWorkflows.Data.Models;
using DataWorkflows.Data.Repositories;
using DataWorkflows.Engine.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Engine.Presentation.Controllers;

/// <summary>
/// Admin API for internal connector operations.
/// Used by connectors to register their actions and by admins to manage the registry.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IActionCatalogRepository _catalogRepository;
    private readonly IActionCatalogRegistry _actionRegistry;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IActionCatalogRepository catalogRepository,
        IActionCatalogRegistry actionRegistry,
        ILogger<AdminController> logger)
    {
        _catalogRepository = catalogRepository ?? throw new ArgumentNullException(nameof(catalogRepository));
        _actionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register actions from a connector.
    /// This endpoint is called by connectors on startup to register their available actions.
    /// The operation is idempotent - calling it multiple times with the same data has the same effect.
    /// </summary>
    /// <param name="request">The registration request containing connector ID and actions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Registration summary.</returns>
    [HttpPost("actions/register")]
    public async Task<IActionResult> RegisterActions(
        [FromBody] RegisterActionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body cannot be null." });

        if (string.IsNullOrWhiteSpace(request.ConnectorId))
            return BadRequest(new { error = "ConnectorId is required." });

        if (request.Actions == null || request.Actions.Count == 0)
            return BadRequest(new { error = "At least one action must be provided." });

        try
        {
            _logger.LogInformation(
                "Received action registration request from connector '{ConnectorId}' with {Count} actions.",
                request.ConnectorId,
                request.Actions.Count);

            // Validate that all action types match the connector ID convention
            var invalidActions = request.Actions
                .Where(a => !a.ActionType.StartsWith(request.ConnectorId + ".", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (invalidActions.Any())
            {
                return BadRequest(new
                {
                    error = "All action types must start with the connector ID.",
                    invalidActions = invalidActions.Select(a => a.ActionType).ToList()
                });
            }

            // Convert request actions to catalog entries
            var catalogEntries = request.Actions.Select(a => new ActionCatalogEntry
            {
                ActionType = a.ActionType,
                ConnectorId = request.ConnectorId,
                DisplayName = a.DisplayName,
                Description = a.Description,
                ParameterSchemaJson = a.ParameterSchema,
                OutputSchemaJson = a.OutputSchema,
                IsEnabled = a.IsEnabled,
                RequiresAuth = a.RequiresAuth
            }).ToList();

            // Upsert actions in the database
            var upsertedCount = await _catalogRepository.UpsertActionsAsync(
                request.ConnectorId,
                catalogEntries,
                cancellationToken);

            // Refresh the action registry cache
            await _actionRegistry.RefreshAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully registered {Count} actions from connector '{ConnectorId}'.",
                upsertedCount,
                request.ConnectorId);

            return Ok(new
            {
                message = $"Successfully registered {upsertedCount} actions from connector '{request.ConnectorId}'.",
                connectorId = request.ConnectorId,
                actionsRegistered = upsertedCount,
                actionTypes = request.Actions.Select(a => a.ActionType).ToList(),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register actions from connector '{ConnectorId}'.", request.ConnectorId);
            return StatusCode(500, new
            {
                error = "Failed to register actions.",
                details = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Manually refresh the ActionCatalogRegistry cache from the database.
    /// Useful after database changes or for troubleshooting.
    /// </summary>
    [HttpPost("actions/refresh")]
    public async Task<IActionResult> RefreshActionCatalogRegistry(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manual ActionCatalogRegistry refresh requested.");

            var lastRefreshedBefore = _actionRegistry.LastRefreshedAt;
            await _actionRegistry.RefreshAsync(cancellationToken);
            var actionCount = _actionRegistry.GetAllActions().Count;

            return Ok(new
            {
                message = "ActionCatalogRegistry cache refreshed successfully.",
                actionCount,
                lastRefreshedAt = _actionRegistry.LastRefreshedAt,
                previousRefreshAt = lastRefreshedBefore,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh ActionCatalogRegistry.");
            return StatusCode(500, new
            {
                error = "Failed to refresh ActionCatalogRegistry.",
                details = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get information about the current ActionCatalogRegistry state.
    /// </summary>
    [HttpGet("actions/registry")]
    public IActionResult GetCatalogRegistryInfo()
    {
        var actions = _actionRegistry.GetAllActions();
        var groupedByConnector = actions
            .GroupBy(a => a.ConnectorId)
            .Select(g => new
            {
                connectorId = g.Key,
                actionCount = g.Count(),
                actionTypes = g.Select(a => a.ActionType).OrderBy(x => x).ToList()
            })
            .OrderBy(x => x.connectorId)
            .ToList();

        return Ok(new
        {
            totalActions = actions.Count,
            lastRefreshedAt = _actionRegistry.LastRefreshedAt,
            connectors = groupedByConnector,
            timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Request model for action registration.
/// </summary>
public sealed record RegisterActionsRequest
{
    public required string ConnectorId { get; init; }
    public required List<ActionRegistrationDto> Actions { get; init; }
}

/// <summary>
/// DTO for a single action registration.
/// </summary>
public sealed record ActionRegistrationDto
{
    public required string ActionType { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string ParameterSchema { get; init; }
    public required string OutputSchema { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool RequiresAuth { get; init; } = true;
}
