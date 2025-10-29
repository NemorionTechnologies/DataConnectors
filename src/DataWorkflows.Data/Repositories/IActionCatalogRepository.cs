using DataWorkflows.Data.Models;

namespace DataWorkflows.Data.Repositories;

/// <summary>
/// Repository for managing the ActionCatalog table.
/// Provides operations for action registration, lookup, and querying.
/// </summary>
public interface IActionCatalogRepository
{
    /// <summary>
    /// Upserts (insert or update) a collection of actions for a given connector.
    /// This operation is idempotent - calling it multiple times with the same data has the same effect.
    /// Uses (ConnectorId, ActionType) as the unique key for upsert.
    /// </summary>
    /// <param name="connectorId">The connector identifier (e.g., "monday").</param>
    /// <param name="actions">The actions to register or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of actions upserted (inserted or updated).</returns>
    Task<int> UpsertActionsAsync(
        string connectorId,
        IEnumerable<ActionCatalogEntry> actions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an action by its ActionType.
    /// Returns null if the action doesn't exist.
    /// </summary>
    /// <param name="actionType">The action type (e.g., "monday.get-items").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The action catalog entry, or null if not found.</returns>
    Task<ActionCatalogEntry?> GetByActionTypeAsync(
        string actionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all actions registered by a specific connector.
    /// </summary>
    /// <param name="connectorId">The connector identifier.</param>
    /// <param name="includeDisabled">Whether to include disabled actions. Default is false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of actions for the specified connector.</returns>
    Task<IReadOnlyList<ActionCatalogEntry>> GetByConnectorIdAsync(
        string connectorId,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (enabled) actions from all connectors.
    /// This is used to populate the ActionRegistry cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of all active actions.</returns>
    Task<IReadOnlyList<ActionCatalogEntry>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all actions, optionally filtering by enabled status.
    /// </summary>
    /// <param name="isEnabled">Optional filter for enabled status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of actions matching the filter.</returns>
    Task<IReadOnlyList<ActionCatalogEntry>> GetAllAsync(
        bool? isEnabled = null,
        CancellationToken cancellationToken = default);
}
