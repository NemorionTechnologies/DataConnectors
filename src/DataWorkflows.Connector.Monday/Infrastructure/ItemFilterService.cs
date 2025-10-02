using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure;

/// <summary>
/// Service for filtering Monday.com items based on various criteria.
/// </summary>
public class ItemFilterService : IItemFilterService
{
    private readonly IColumnValueParser _columnValueParser;
    private readonly IMondayApiClient _apiClient;
    private readonly ILogger<ItemFilterService> _logger;

    public ItemFilterService(
        IColumnValueParser columnValueParser,
        IMondayApiClient apiClient,
        ILogger<ItemFilterService> logger)
    {
        _columnValueParser = columnValueParser;
        _apiClient = apiClient;
        _logger = logger;
    }

    public IEnumerable<MondayItemDto> FilterByTimelineEndDate(
        IEnumerable<MondayItemDto> items,
        string timelineColumnId,
        DateTime afterDate)
    {
        if (string.IsNullOrWhiteSpace(timelineColumnId))
        {
            throw new ArgumentException("Timeline column ID cannot be null or empty", nameof(timelineColumnId));
        }

        var filteredItems = new List<MondayItemDto>();

        foreach (var item in items)
        {
            var endDate = GetTimelineEndDate(item, timelineColumnId);

            if (endDate.HasValue && endDate.Value > afterDate)
            {
                filteredItems.Add(item);
                _logger.LogDebug(
                    "Item {ItemId} ({ItemTitle}) included - timeline ends {EndDate}",
                    item.Id,
                    item.Title,
                    endDate.Value);
            }
        }

        _logger.LogInformation(
            "Filtered {FilteredCount} items out of {TotalCount} with timeline ending after {CutoffDate}",
            filteredItems.Count,
            items.Count(),
            afterDate);

        return filteredItems;
    }

    public DateTime? GetTimelineEndDate(MondayItemDto item, string timelineColumnId)
    {
        if (!item.ColumnValues.TryGetValue(timelineColumnId, out var timelineColumn))
        {
            return null;
        }

        var timeline = _columnValueParser.ParseTimeline(timelineColumn);
        return timeline?.To;
    }

    public async Task<IReadOnlyList<ItemWithMetadata>> GetItemsWithRecentUpdatesAsync(
        IEnumerable<MondayHydratedItemDto> hydratedItems,
        string statusColumnId,
        string timelineColumnId,
        DateTime sinceDate,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ItemWithMetadata>();

        foreach (var item in hydratedItems)
        {
            // Process main item
            var itemLastUpdate = GetLastUpdateDate(item.Updates);

            if (itemLastUpdate.HasValue && itemLastUpdate.Value >= sinceDate)
            {
                results.Add(CreateItemWithMetadata(
                    item.Id,
                    item.Title,
                    ItemType.Item,
                    item.ParentId,
                    item.ColumnValues,
                    statusColumnId,
                    timelineColumnId,
                    itemLastUpdate));
            }

            // Process subitems
            if (item.SubItems?.Any() == true)
            {
                foreach (var subItem in item.SubItems)
                {
                    var subItemUpdates = await _apiClient.GetItemUpdatesAsync(
                        subItem.Id,
                        null,
                        null,
                        cancellationToken);

                    var subItemLastUpdate = GetLastUpdateDate(subItemUpdates);

                    if (subItemLastUpdate.HasValue && subItemLastUpdate.Value >= sinceDate)
                    {
                        results.Add(CreateItemWithMetadata(
                            subItem.Id,
                            subItem.Title,
                            ItemType.SubItem,
                            subItem.ParentId,
                            subItem.ColumnValues,
                            statusColumnId,
                            timelineColumnId,
                            subItemLastUpdate));
                    }
                }
            }
        }

        _logger.LogInformation(
            "Found {Count} items/subitems with updates since {SinceDate}",
            results.Count,
            sinceDate);

        return results.OrderByDescending(r => r.LastUpdateDate).ToList();
    }

    private DateTime? GetLastUpdateDate(IEnumerable<MondayUpdateDto>? updates)
    {
        return updates?.Any() == true
            ? updates.Max(u => u.CreatedAt).DateTime
            : null;
    }

    private ItemWithMetadata CreateItemWithMetadata(
        string id,
        string title,
        ItemType type,
        string? parentId,
        Dictionary<string, MondayColumnValueDto> columnValues,
        string statusColumnId,
        string timelineColumnId,
        DateTime? lastUpdateDate)
    {
        var status = _columnValueParser.GetTextValue(
            columnValues.GetValueOrDefault(statusColumnId),
            "No Status");

        var timeline = _columnValueParser.ParseTimeline(
            columnValues.GetValueOrDefault(timelineColumnId));

        return new ItemWithMetadata
        {
            Id = id,
            Title = title,
            Type = type,
            ParentId = parentId,
            Status = status,
            Timeline = timeline,
            LastUpdateDate = lastUpdateDate,
            RawColumns = columnValues
        };
    }
}
