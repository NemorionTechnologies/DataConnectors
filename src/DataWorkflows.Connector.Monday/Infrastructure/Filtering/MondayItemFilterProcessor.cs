using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;

namespace DataWorkflows.Connector.Monday.Infrastructure.Filtering;

internal class MondayItemFilterProcessor
{
    private readonly Func<string, MondayFilterDefinition?, CancellationToken, Task<IEnumerable<MondayItemDto>>> _getSubItemsAsync;
    private readonly Func<string, DateTime?, DateTime?, CancellationToken, Task<IEnumerable<MondayUpdateDto>>> _getItemUpdatesAsync;

    public MondayItemFilterProcessor(
        Func<string, MondayFilterDefinition?, CancellationToken, Task<IEnumerable<MondayItemDto>>> getSubItemsAsync,
        Func<string, DateTime?, DateTime?, CancellationToken, Task<IEnumerable<MondayUpdateDto>>> getItemUpdatesAsync)
    {
        _getSubItemsAsync = getSubItemsAsync;
        _getItemUpdatesAsync = getItemUpdatesAsync;
    }

    public async Task<List<MondayItemDto>> ApplySubItemFilterAsync(
        List<MondayItemDto> parentItems,
        MondaySubItemFilterTranslation translation,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, IReadOnlyList<MondayActivityLogDto>>? activityLogLookup)
    {
        if (parentItems.Count == 0)
        {
            return parentItems;
        }

        var evaluations = await Task.WhenAll(parentItems.Select(async item =>
        {
            var subItems = await _getSubItemsAsync(item.Id, MondayFilterDefinition.Empty, cancellationToken);
            var materialized = subItems as IList<MondayItemDto> ?? subItems.ToList();

            if (materialized.Count == 0)
            {
                return (Item: item, Include: false);
            }

            var itemPredicate = translation.ItemPredicate ?? (_ => true);
            var updatePredicate = translation.UpdatePredicate;
            var activityPredicate = translation.ActivityLogPredicate;
            var matches = new List<MondayItemDto>();

            foreach (var subItem in materialized)
            {
                if (string.IsNullOrEmpty(subItem.Id))
                {
                    continue;
                }

                if (!itemPredicate(subItem))
                {
                    continue;
                }

                if (updatePredicate is not null)
                {
                    var updates = await _getItemUpdatesAsync(subItem.Id, null, null, cancellationToken);
                    var materializedUpdates = updates as IList<MondayUpdateDto> ?? updates.ToList();

                    if (!updatePredicate(materializedUpdates))
                    {
                        continue;
                    }
                }

                if (activityPredicate is not null)
                {
                    var logs = activityLogLookup is not null && subItem.Id is not null && activityLogLookup.TryGetValue(subItem.Id, out var subItemLogs)
                        ? subItemLogs
                        : Array.Empty<MondayActivityLogDto>();

                    if (!activityPredicate(logs))
                    {
                        continue;
                    }
                }

                matches.Add(subItem);
            }

            var include = translation.Mode switch
            {
                MondayAggregationMode.All => matches.Count == materialized.Count,
                _ => matches.Count > 0
            };

            return (Item: item, Include: include);
        }));

        return evaluations
            .Where(result => result.Include)
            .Select(result => result.Item)
            .ToList();
    }

    public async Task<List<MondayItemDto>> ApplyUpdateFilterAsync(
        List<MondayItemDto> parentItems,
        Func<IEnumerable<MondayUpdateDto>, bool> updatePredicate,
        CancellationToken cancellationToken)
    {
        if (parentItems.Count == 0)
        {
            return parentItems;
        }

        var evaluations = await Task.WhenAll(parentItems.Select(async item =>
        {
            var updates = await _getItemUpdatesAsync(item.Id, null, null, cancellationToken);
            var materialized = updates as IList<MondayUpdateDto> ?? updates.ToList();
            var include = updatePredicate(materialized);
            return (Item: item, Include: include);
        }));

        return evaluations
            .Where(result => result.Include)
            .Select(result => result.Item)
            .ToList();
    }

    public List<MondayItemDto> ApplyActivityLogFilter(
        List<MondayItemDto> items,
        Func<IEnumerable<MondayActivityLogDto>, bool> activityPredicate,
        IReadOnlyDictionary<string, IReadOnlyList<MondayActivityLogDto>>? activityLogLookup)
    {
        if (items.Count == 0)
        {
            return items;
        }

        return items
            .Where(item =>
            {
                var logs = activityLogLookup is not null && activityLogLookup.TryGetValue(item.Id, out var itemLogs)
                    ? itemLogs
                    : Array.Empty<MondayActivityLogDto>();

                return activityPredicate(logs);
            })
            .ToList();
    }
}
