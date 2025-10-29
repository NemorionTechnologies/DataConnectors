using System.Collections.Concurrent;
using DataWorkflows.Data.Models;
using DataWorkflows.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Engine.Core.Services;

/// <summary>
/// Thread-safe in-memory cache of action catalog entries.
/// Loaded from ActionCatalog table on startup and refreshed periodically.
/// This is distinct from the execution ActionRegistry which holds IWorkflowAction instances.
/// </summary>
public sealed class ActionCatalogRegistry : IActionCatalogRegistry
{
    private readonly IActionCatalogRepository _repository;
    private readonly ILogger<ActionCatalogRegistry> _logger;
    private readonly ConcurrentDictionary<string, ActionCatalogEntry> _actions = new();
    private DateTime _lastRefreshedAt = DateTime.MinValue;

    public ActionCatalogRegistry(
        IActionCatalogRepository repository,
        ILogger<ActionCatalogRegistry> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ActionCatalogEntry? GetAction(string actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
            return null;

        return _actions.TryGetValue(actionType, out var action) ? action : null;
    }

    public IReadOnlyList<ActionCatalogEntry> GetAllActions()
    {
        return _actions.Values.ToList();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Refreshing ActionCatalogRegistry cache from database...");

            var activeActions = await _repository.GetAllActiveAsync(cancellationToken);

            // Clear and rebuild the cache
            _actions.Clear();
            foreach (var action in activeActions)
            {
                _actions[action.ActionType] = action;
            }

            _lastRefreshedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "ActionCatalogRegistry cache refreshed. Loaded {Count} active actions.",
                activeActions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh ActionCatalogRegistry cache.");
            throw;
        }
    }

    public DateTime LastRefreshedAt => _lastRefreshedAt;
}
