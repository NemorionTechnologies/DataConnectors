using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DataWorkflows.Connector.Monday.Infrastructure;

/// <summary>
/// In-memory implementation of column metadata cache using IMemoryCache.
/// Thread-safe and supports TTL-based expiration.
/// </summary>
public class InMemoryColumnMetadataCache : IColumnMetadataCache
{
    private readonly IMemoryCache _cache;
    private readonly HashSet<string> _cachedBoardIds = new();
    private readonly object _lockObject = new();

    public InMemoryColumnMetadataCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<IReadOnlyList<ColumnMetadata>?> GetBoardColumnsAsync(string boardId)
    {
        var cacheKey = GetCacheKey(boardId);
        _cache.TryGetValue(cacheKey, out IReadOnlyList<ColumnMetadata>? columns);
        return Task.FromResult(columns);
    }

    public Task SetBoardColumnsAsync(string boardId, IReadOnlyList<ColumnMetadata> columns, TimeSpan ttl)
    {
        var cacheKey = GetCacheKey(boardId);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        _cache.Set(cacheKey, columns, options);

        lock (_lockObject)
        {
            _cachedBoardIds.Add(boardId);
        }

        return Task.CompletedTask;
    }

    public Task FlushBoardAsync(string boardId)
    {
        var cacheKey = GetCacheKey(boardId);
        _cache.Remove(cacheKey);

        lock (_lockObject)
        {
            _cachedBoardIds.Remove(boardId);
        }

        return Task.CompletedTask;
    }

    public Task FlushAllAsync()
    {
        List<string> boardIdsToRemove;

        lock (_lockObject)
        {
            boardIdsToRemove = _cachedBoardIds.ToList();
            _cachedBoardIds.Clear();
        }

        foreach (var boardId in boardIdsToRemove)
        {
            var cacheKey = GetCacheKey(boardId);
            _cache.Remove(cacheKey);
        }

        return Task.CompletedTask;
    }

    private static string GetCacheKey(string boardId) => $"board_columns_{boardId}";
}
