using DataWorkflows.Data.Models;

namespace DataWorkflows.Engine.Core.Services;

/// <summary>
/// In-memory registry of available workflow actions from the ActionCatalog table.
/// Caches ActionCatalog entries for fast lookup during validation and execution.
/// This is distinct from the execution ActionRegistry which holds IWorkflowAction instances.
/// </summary>
public interface IActionCatalogRegistry
{
    /// <summary>
    /// Gets an action by its ActionType.
    /// Returns null if the action is not registered or not enabled.
    /// </summary>
    /// <param name="actionType">The action type (e.g., "monday.get-items").</param>
    /// <returns>The action catalog entry, or null if not found.</returns>
    ActionCatalogEntry? GetAction(string actionType);

    /// <summary>
    /// Gets all registered actions.
    /// </summary>
    /// <returns>Collection of all registered actions.</returns>
    IReadOnlyList<ActionCatalogEntry> GetAllActions();

    /// <summary>
    /// Refreshes the registry cache from the database.
    /// This is called periodically and can also be triggered manually via admin API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the last cache refresh.
    /// </summary>
    DateTime LastRefreshedAt { get; }
}
