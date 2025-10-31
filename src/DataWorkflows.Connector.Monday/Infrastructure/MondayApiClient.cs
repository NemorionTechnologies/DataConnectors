using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Domain.Exceptions;
using DataWorkflows.Connector.Monday.Infrastructure.Filtering;
using DataWorkflows.Connector.Monday.Infrastructure.GraphQL;
using DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;
using DataWorkflows.Connector.Monday.Infrastructure.Mapping;
using DataWorkflows.Connector.Monday.Infrastructure.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Connector.Monday.Infrastructure;

public class MondayApiClient : IMondayApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MondayApiClient> _logger;
    private readonly IMondayFilterTranslator _filterTranslator;
    private readonly MondayGraphQLQueryBuilder _queryBuilder;
    private readonly MondayGraphQLExecutor _graphQlExecutor;
    private readonly MondayResponseMapper _responseMapper;
    private readonly MondayItemFilterProcessor _itemFilterProcessor;

    public MondayApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MondayApiClient> logger,
        IMondayFilterTranslator filterTranslator)
    {
        _httpClient = httpClient;
        _logger = logger;
        _filterTranslator = filterTranslator;

        var apiKey = configuration["Monday:ApiKey"] ?? throw new InvalidOperationException("Monday API key not configured");
        _httpClient.BaseAddress = new Uri("https://api.monday.com/v2/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Add("API-Version", "2024-10");

        _queryBuilder = new MondayGraphQLQueryBuilder();
        _graphQlExecutor = new MondayGraphQLExecutor(_httpClient, _logger);

        var activityLogParser = new ActivityLogParser();
        _responseMapper = new MondayResponseMapper(activityLogParser);
        _itemFilterProcessor = new MondayItemFilterProcessor(GetSubItemsAsync, GetItemUpdatesAsync);
    }

    public async Task<IEnumerable<MondayItemDto>> GetBoardItemsAsync(
        string boardId,
        MondayFilterDefinition? filter,
        CancellationToken cancellationToken)
    {
        var translation = _filterTranslator.Translate(filter);
        var query = _queryBuilder.BuildGetBoardItemsQuery(boardId, filter, translation.QueryParams);
        var response = await _graphQlExecutor.ExecuteQueryAsync<BoardItemsResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        var itemsPage = response.Data.Boards.First().ItemsPage;
        if (itemsPage?.Items == null)
        {
            return Enumerable.Empty<MondayItemDto>();
        }

        var items = itemsPage.Items
            .Select(item => _responseMapper.MapToMondayItemDto((JsonElement)item))
            .ToList();

        if (translation.ClientPredicate is not null)
        {
            items = items.Where(translation.ClientPredicate).ToList();
        }

        IReadOnlyDictionary<string, IReadOnlyList<MondayActivityLogDto>>? activityLogLookup = null;
        if (translation.ActivityLogPredicate is not null ||
            translation.SubItemTranslation?.ActivityLogPredicate is not null)
        {
            var activityLogs = (await GetBoardActivityAsync(boardId, null, null, cancellationToken)).ToList();
            activityLogLookup = activityLogs
                .Where(log => !string.IsNullOrWhiteSpace(log.ItemId))
                .GroupBy(log => log.ItemId!)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<MondayActivityLogDto>)group.ToList());
        }

        if (translation.SubItemTranslation is not null)
        {
            items = await _itemFilterProcessor.ApplySubItemFilterAsync(
                items,
                translation.SubItemTranslation,
                cancellationToken,
                activityLogLookup);
        }

        if (translation.UpdatePredicate is not null)
        {
            items = await _itemFilterProcessor.ApplyUpdateFilterAsync(
                items,
                translation.UpdatePredicate,
                cancellationToken);
        }

        if (translation.ActivityLogPredicate is not null)
        {
            items = _itemFilterProcessor.ApplyActivityLogFilter(
                items,
                translation.ActivityLogPredicate,
                activityLogLookup);
        }

        return items;
    }

    public async Task<IEnumerable<MondayActivityLogDto>> GetBoardActivityAsync(
        string boardId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.BuildGetBoardActivityQuery(boardId, fromDate, toDate);
        var response = await _graphQlExecutor.ExecuteQueryAsync<BoardActivityResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        return response.Data.Boards.First().ActivityLogs
            .Select(log => _responseMapper.MapToMondayActivityLogDto((JsonElement)log))
            .ToList();
    }

    public async Task<IEnumerable<MondayUpdateDto>> GetBoardUpdatesAsync(
        string boardId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.BuildGetBoardUpdatesQuery(boardId, fromDate, toDate);
        var response = await _graphQlExecutor.ExecuteQueryAsync<BoardUpdatesResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        var updates = new List<MondayUpdateDto>();
        foreach (var board in response.Data.Boards)
        {
            if (board.ItemsPage?.Items == null)
            {
                continue;
            }

            foreach (var item in board.ItemsPage.Items)
            {
                foreach (var update in item.Updates)
                {
                    updates.Add(_responseMapper.MapToMondayUpdateDto((JsonElement)update, item.Id));
                }
            }
        }

        return updates;
    }

    public async Task<IEnumerable<MondayItemDto>> GetSubItemsAsync(
        string parentItemId,
        MondayFilterDefinition? filter,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.BuildGetSubItemsQuery(parentItemId);
        var response = await _graphQlExecutor.ExecuteQueryAsync<ItemSubItemsResponse>(query, cancellationToken);

        if (response?.Data?.Items == null || !response.Data.Items.Any())
        {
            throw new ResourceNotFoundException("Item", parentItemId);
        }

        var translation = _filterTranslator.Translate(filter);
        var subItems = response.Data.Items.First().SubItems
            .Select(subItem => _responseMapper.MapToMondayItemDto((JsonElement)subItem))
            .ToList();

        if (translation.ClientPredicate is not null)
        {
            subItems = subItems.Where(translation.ClientPredicate).ToList();
        }

        return subItems;
    }

    public async Task<IEnumerable<MondayUpdateDto>> GetItemUpdatesAsync(
        string itemId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.BuildGetItemUpdatesQuery(itemId, fromDate, toDate);
        var response = await _graphQlExecutor.ExecuteQueryAsync<ItemUpdatesResponse>(query, cancellationToken);

        if (response?.Data?.Items == null || !response.Data.Items.Any())
        {
            throw new ResourceNotFoundException("Item", itemId);
        }

        return response.Data.Items.First().Updates
            .Select(update => _responseMapper.MapToMondayUpdateDto((JsonElement)update, itemId))
            .ToList();
    }

    public async Task<IEnumerable<MondayHydratedItemDto>> GetHydratedBoardItemsAsync(
        string boardId,
        MondayFilterDefinition? filter,
        CancellationToken cancellationToken)
    {
        var parentItems = (await GetBoardItemsAsync(boardId, filter, cancellationToken)).ToList();

        var hydratedItemsTasks = parentItems.Select(async item =>
        {
            var subItemsTask = GetSubItemsAsync(item.Id, MondayFilterDefinition.Empty, cancellationToken);
            var updatesTask = GetItemUpdatesAsync(item.Id, null, null, cancellationToken);

            await Task.WhenAll(subItemsTask, updatesTask);

            return new MondayHydratedItemDto
            {
                Id = item.Id,
                ParentId = item.ParentId,
                Title = item.Title,
                GroupId = item.GroupId,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                ColumnValues = item.ColumnValues,
                SubItems = await subItemsTask,
                Updates = await updatesTask
            };
        });

        return await Task.WhenAll(hydratedItemsTasks);
    }

    public async Task<MondayItemDto> UpdateColumnValueAsync(
        string boardId,
        string itemId,
        string columnId,
        string valueJson,
        CancellationToken cancellationToken)
    {
        var mutation = _queryBuilder.BuildUpdateColumnValueMutation(boardId, itemId, columnId, valueJson);
        var response = await _graphQlExecutor.ExecuteQueryAsync<UpdateColumnValueResponse>(mutation, cancellationToken);

        if (response?.Data?.ChangeColumnValue is not JsonElement changeColumnValue ||
            changeColumnValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new ResourceNotFoundException("Item or Column", $"{itemId}/{columnId}");
        }

        return _responseMapper.MapToMondayItemDto(changeColumnValue);
    }

    public async Task<MondayItemDto> CreateItemAsync(
        string boardId,
        string itemName,
        string? groupId,
        Dictionary<string, object>? columnValues,
        CancellationToken cancellationToken)
    {
        var mutation = _queryBuilder.BuildCreateItemMutation(boardId, itemName, groupId, columnValues);
        var response = await _graphQlExecutor.ExecuteQueryAsync<CreateItemResponse>(mutation, cancellationToken);

        if (response?.Data?.CreateItem is not JsonElement createdItem ||
            createdItem.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Failed to create item '{itemName}' on board {boardId}");
        }

        _logger.LogInformation("Created item {ItemId} on board {BoardId}",
            createdItem.TryGetProperty("id", out var id) ? id.GetString() : "unknown",
            boardId);

        return _responseMapper.MapToMondayItemDto(createdItem);
    }

    public async Task<MondayItemDto> CreateSubItemAsync(
        string parentItemId,
        string itemName,
        Dictionary<string, object>? columnValues,
        CancellationToken cancellationToken)
    {
        var mutation = _queryBuilder.BuildCreateSubItemMutation(parentItemId, itemName, columnValues);
        var response = await _graphQlExecutor.ExecuteQueryAsync<CreateSubItemResponse>(mutation, cancellationToken);

        if (response?.Data?.CreateSubItem is not JsonElement createdSubItem ||
            createdSubItem.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Failed to create sub-item '{itemName}' under parent {parentItemId}");
        }

        _logger.LogInformation("Created sub-item {ItemId} under parent {ParentItemId}",
            createdSubItem.TryGetProperty("id", out var id) ? id.GetString() : "unknown",
            parentItemId);

        return _responseMapper.MapToMondayItemDto(createdSubItem);
    }

    public async Task<IReadOnlyList<ColumnMetadata>> GetBoardColumnsAsync(
        string boardId,
        CancellationToken cancellationToken)
    {
        var query = _queryBuilder.BuildGetBoardColumnsQuery(boardId);
        var response = await _graphQlExecutor.ExecuteQueryAsync<BoardColumnsResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        var columns = response.Data.Boards.First().Columns;
        if (columns == null || !columns.Any())
        {
            return Array.Empty<ColumnMetadata>();
        }

        return columns
            .Select(column => _responseMapper.MapToColumnMetadata((JsonElement)column))
            .ToList();
    }
}
