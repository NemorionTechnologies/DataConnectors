using DataWorkflows.Engine.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Engine.Presentation.Services;

/// <summary>
/// Background service that initializes the ActionCatalogRegistry on startup
/// and refreshes it periodically.
/// </summary>
public sealed class ActionCatalogRegistryInitializer : BackgroundService
{
    private readonly IActionCatalogRegistry _registry;
    private readonly ILogger<ActionCatalogRegistryInitializer> _logger;
    private readonly TimeSpan _refreshInterval;

    public ActionCatalogRegistryInitializer(
        IActionCatalogRegistry registry,
        ILogger<ActionCatalogRegistryInitializer> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _refreshInterval = TimeSpan.FromMinutes(5); // Refresh every 5 minutes
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ActionCatalogRegistryInitializer starting...");

        // Initial load
        try
        {
            await _registry.RefreshAsync(stoppingToken);
            _logger.LogInformation("ActionCatalogRegistry initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ActionCatalogRegistry. Will retry on next interval.");
        }

        // Periodic refresh
        using var timer = new PeriodicTimer(_refreshInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await _registry.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic ActionCatalogRegistry refresh.");
            }
        }

        _logger.LogInformation("ActionCatalogRegistryInitializer stopped.");
    }
}
