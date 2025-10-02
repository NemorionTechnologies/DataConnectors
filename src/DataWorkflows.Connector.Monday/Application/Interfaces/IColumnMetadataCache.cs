using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

/// <summary>
/// Cache for Monday.com board column metadata.
/// Abstracts the caching layer to allow for future implementation swaps.
/// </summary>
public interface IColumnMetadataCache
{
    /// <summary>
    /// Gets all column metadata for a specific board.
    /// </summary>
    /// <param name="boardId">The board ID to get columns for</param>
    /// <returns>List of column metadata, or null if not cached</returns>
    Task<IReadOnlyList<ColumnMetadata>?> GetBoardColumnsAsync(string boardId);

    /// <summary>
    /// Stores column metadata for a specific board with a TTL.
    /// </summary>
    /// <param name="boardId">The board ID</param>
    /// <param name="columns">The column metadata to cache</param>
    /// <param name="ttl">Time-to-live for the cache entry</param>
    Task SetBoardColumnsAsync(string boardId, IReadOnlyList<ColumnMetadata> columns, TimeSpan ttl);

    /// <summary>
    /// Flushes (removes) cached column metadata for a specific board.
    /// </summary>
    /// <param name="boardId">The board ID to flush</param>
    Task FlushBoardAsync(string boardId);

    /// <summary>
    /// Flushes (removes) all cached column metadata for all boards.
    /// </summary>
    Task FlushAllAsync();
}
