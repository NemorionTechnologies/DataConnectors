using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Domain.Exceptions;

namespace DataWorkflows.Connector.Monday.Infrastructure;

/// <summary>
/// Service that resolves column titles to column IDs using cached column metadata.
/// Implements case-insensitive title matching and automatic cache population.
/// </summary>
public class ColumnResolverService : IColumnResolverService
{
    private readonly IMondayApiClient _apiClient;
    private readonly IColumnMetadataCache _cache;
    private readonly ILogger<ColumnResolverService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public ColumnResolverService(
        IMondayApiClient apiClient,
        IColumnMetadataCache cache,
        ILogger<ColumnResolverService> logger)
    {
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> ResolveColumnIdAsync(
        string boardId,
        string? columnId,
        string? columnTitle,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(columnId) && string.IsNullOrWhiteSpace(columnTitle))
        {
            throw new ArgumentException("Either columnId or columnTitle must be provided");
        }

        if (!string.IsNullOrWhiteSpace(columnId))
        {
            _logger.LogDebug("Using provided columnId: {ColumnId}", columnId);
            return columnId;
        }

        _logger.LogDebug("Resolving columnTitle '{ColumnTitle}' for board {BoardId}", columnTitle, boardId);

        var columns = await GetOrFetchColumnsAsync(boardId, cancellationToken);

        var matchedColumn = columns.FirstOrDefault(c =>
            string.Equals(c.Title, columnTitle, StringComparison.OrdinalIgnoreCase));

        if (matchedColumn == null)
        {
            var availableColumns = string.Join(", ", columns.Select(c => $"'{c.Title}'"));
            var message = $"Column with title '{columnTitle}' not found on board {boardId}. Available columns: {availableColumns}";
            _logger.LogWarning(message);
            throw new ResourceNotFoundException(message);
        }

        _logger.LogDebug("Resolved columnTitle '{ColumnTitle}' to columnId '{ColumnId}'", columnTitle, matchedColumn.Id);
        return matchedColumn.Id;
    }

    private async Task<IReadOnlyList<ColumnMetadata>> GetOrFetchColumnsAsync(
        string boardId,
        CancellationToken cancellationToken)
    {
        var cachedColumns = await _cache.GetBoardColumnsAsync(boardId);
        if (cachedColumns != null)
        {
            _logger.LogDebug("Using cached columns for board {BoardId}", boardId);
            return cachedColumns;
        }

        _logger.LogDebug("Cache miss for board {BoardId}, fetching from API", boardId);
        var columns = await _apiClient.GetBoardColumnsAsync(boardId, cancellationToken);

        await _cache.SetBoardColumnsAsync(boardId, columns, CacheTtl);
        _logger.LogInformation("Cached {Count} columns for board {BoardId} with TTL {Ttl}",
            columns.Count, boardId, CacheTtl);

        return columns;
    }
}
