using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

/// <summary>
/// Service for filtering Monday.com items based on various criteria.
/// </summary>
public interface IItemFilterService
{
    /// <summary>
    /// Filters items by timeline end date, returning only items where the timeline ends after the specified date.
    /// </summary>
    /// <param name="items">Items to filter</param>
    /// <param name="timelineColumnId">The ID of the timeline column to check</param>
    /// <param name="afterDate">The cutoff date - items with timeline ending after this date will be included</param>
    /// <returns>Filtered list of items with their timeline end dates</returns>
    IEnumerable<MondayItemDto> FilterByTimelineEndDate(
        IEnumerable<MondayItemDto> items,
        string timelineColumnId,
        DateTime afterDate);

    /// <summary>
    /// Gets the timeline end date for an item if it has a timeline column.
    /// </summary>
    /// <param name="item">The item to check</param>
    /// <param name="timelineColumnId">The ID of the timeline column</param>
    /// <returns>The timeline end date, or null if not found or invalid</returns>
    DateTime? GetTimelineEndDate(MondayItemDto item, string timelineColumnId);

    /// <summary>
    /// Gets all items and subitems from hydrated items that have updates within the specified date range.
    /// Returns enriched items with status, timeline, and last update metadata.
    /// </summary>
    /// <param name="hydratedItems">Hydrated items with subitems and updates</param>
    /// <param name="statusColumnId">Column ID for status</param>
    /// <param name="timelineColumnId">Column ID for timeline</param>
    /// <param name="sinceDate">Only include items with updates after this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of items with metadata, ordered by last update date descending</returns>
    Task<IReadOnlyList<ItemWithMetadata>> GetItemsWithRecentUpdatesAsync(
        IEnumerable<MondayHydratedItemDto> hydratedItems,
        string statusColumnId,
        string timelineColumnId,
        DateTime sinceDate,
        CancellationToken cancellationToken = default);
}
