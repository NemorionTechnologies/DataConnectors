using DataWorkflows.Contracts.Actions;

namespace DataWorkflows.Engine.Core.Services;

/// <summary>
/// Executes workflow actions on remote connectors via HTTP.
/// Used for external connector actions (e.g., monday.get-items) that are not built into the engine.
/// </summary>
public interface IRemoteActionExecutor
{
    /// <summary>
    /// Executes an action on a remote connector.
    /// </summary>
    /// <param name="connectorId">The connector ID (e.g., "monday").</param>
    /// <param name="actionType">The full action type (e.g., "monday.get-items").</param>
    /// <param name="context">The action execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The action execution result from the remote connector.</returns>
    Task<ActionExecutionResult> ExecuteRemoteActionAsync(
        string connectorId,
        string actionType,
        ActionExecutionContext context,
        CancellationToken cancellationToken);
}
