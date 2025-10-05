using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Domain.Exceptions;

namespace DataWorkflows.Connector.Monday.Infrastructure;

public class MondayApiClient : IMondayApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MondayApiClient> _logger;
    private readonly IMondayFilterTranslator _filterTranslator;
    private readonly string _apiKey;

    public MondayApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MondayApiClient> logger,
        IMondayFilterTranslator filterTranslator)
    {
        _httpClient = httpClient;
        _logger = logger;
        _filterTranslator = filterTranslator;
        _apiKey = configuration["Monday:ApiKey"] ?? throw new InvalidOperationException("Monday API key not configured");

        _httpClient.BaseAddress = new Uri("https://api.monday.com/v2/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("API-Version", "2024-10");
    }

    public async Task<IEnumerable<MondayItemDto>> GetBoardItemsAsync(
        string boardId,
        MondayFilterDefinition? filter,
        CancellationToken cancellationToken)
    {
        var translation = _filterTranslator.Translate(filter);
        var query = BuildGetBoardItemsQuery(boardId, filter, translation.QueryParams);
        var response = await ExecuteGraphQLQueryAsync<BoardItemsResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        var itemsPage = response.Data.Boards.First().ItemsPage;
        if (itemsPage?.Items == null)
        {
            return Enumerable.Empty<MondayItemDto>();
        }

        var items = itemsPage.Items.Select(MapToMondayItemDto).ToList();
        if (translation.ClientPredicate is not null)
        {
            items = items.Where(translation.ClientPredicate).ToList();
        }

        IReadOnlyDictionary<string, IReadOnlyList<MondayActivityLogDto>>? activityLogLookup = null;
        if (translation.ActivityLogPredicate is not null || translation.SubItemTranslation?.ActivityLogPredicate is not null)
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
            items = await ApplySubItemFilterAsync(items, translation.SubItemTranslation, cancellationToken, activityLogLookup);
        }

        if (translation.UpdatePredicate is not null)
        {
            items = await ApplyUpdateFilterAsync(items, translation.UpdatePredicate, cancellationToken);
        }

        if (translation.ActivityLogPredicate is not null)
        {
            items = ApplyActivityLogFilter(items, translation.ActivityLogPredicate, activityLogLookup);
        }

        return items;
    }


    private async Task<List<MondayItemDto>> ApplySubItemFilterAsync(
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
            var subItems = await GetSubItemsAsync(item.Id, MondayFilterDefinition.Empty, cancellationToken);
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
                    var updates = await GetItemUpdatesAsync(subItem.Id, null, null, cancellationToken);
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


    private List<MondayItemDto> ApplyActivityLogFilter(
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
    private async Task<List<MondayItemDto>> ApplyUpdateFilterAsync(
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
            var updates = await GetItemUpdatesAsync(item.Id, null, null, cancellationToken);
            var materialized = updates as IList<MondayUpdateDto> ?? updates.ToList();
            var include = updatePredicate(materialized);
            return (Item: item, Include: include);
        }));

        return evaluations
            .Where(result => result.Include)
            .Select(result => result.Item)
            .ToList();
    }

    public async Task<IEnumerable<MondayActivityLogDto>> GetBoardActivityAsync(
        string boardId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = BuildGetBoardActivityQuery(boardId, fromDate, toDate);
        var response = await ExecuteGraphQLQueryAsync<BoardActivityResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        return response.Data.Boards.First().ActivityLogs.Select(MapToMondayActivityLogDto);
    }

    public async Task<IEnumerable<MondayUpdateDto>> GetBoardUpdatesAsync(
        string boardId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = BuildGetBoardUpdatesQuery(boardId, fromDate, toDate);
        var response = await ExecuteGraphQLQueryAsync<BoardUpdatesResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        var updates = new List<MondayUpdateDto>();
        foreach (var board in response.Data.Boards)
        {
            if (board.ItemsPage?.Items != null)
            {
                foreach (var item in board.ItemsPage.Items)
                {
                    foreach (var update in item.Updates)
                    {
                        updates.Add(MapToMondayUpdateDto(update, item.Id));
                    }
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
        var query = BuildGetSubItemsQuery(parentItemId);
        var response = await ExecuteGraphQLQueryAsync<ItemSubItemsResponse>(query, cancellationToken);

        if (response?.Data?.Items == null || !response.Data.Items.Any())
        {
            throw new ResourceNotFoundException("Item", parentItemId);
        }

        var translation = _filterTranslator.Translate(filter);
        var subItems = response.Data.Items.First().SubItems.Select(MapToMondayItemDto).ToList();

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
        var query = BuildGetItemUpdatesQuery(itemId, fromDate, toDate);
        var response = await ExecuteGraphQLQueryAsync<ItemUpdatesResponse>(query, cancellationToken);

        if (response?.Data?.Items == null || !response.Data.Items.Any())
        {
            throw new ResourceNotFoundException("Item", itemId);
        }

        var updates = new List<MondayUpdateDto>();
        foreach (var update in response.Data.Items.First().Updates)
        {
            updates.Add(MapToMondayUpdateDto(update, itemId));
        }
        return updates;
    }

    public async Task<IEnumerable<MondayHydratedItemDto>> GetHydratedBoardItemsAsync(
        string boardId,
        MondayFilterDefinition? filter,
        CancellationToken cancellationToken)
    {
        // First, get the parent items
        var parentItems = (await GetBoardItemsAsync(boardId, filter, cancellationToken)).ToList();

        // Then, concurrently fetch sub-items and updates for each parent item
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
        var mutation = BuildUpdateColumnValueMutation(boardId, itemId, columnId, valueJson);
        var response = await ExecuteGraphQLQueryAsync<UpdateColumnValueResponse>(mutation, cancellationToken);

        if (response?.Data?.ChangeColumnValue is not JsonElement changeColumnValue)
        {
            throw new ResourceNotFoundException("Item or Column", $"{itemId}/{columnId}");
        }

        if (changeColumnValue.ValueKind == JsonValueKind.Null || changeColumnValue.ValueKind == JsonValueKind.Undefined)
        {
            throw new ResourceNotFoundException("Item or Column", $"{itemId}/{columnId}");
        }

        return MapToMondayItemDto(changeColumnValue);
    }

    private async Task<T?> ExecuteGraphQLQueryAsync<T>(string query, CancellationToken cancellationToken)

    {

        var request = new { query };

        var json = JsonSerializer.Serialize(request);

        var content = new StringContent(json, Encoding.UTF8, "application/json");



        _logger.LogDebug("Executing GraphQL query: {Query}", query);



        var httpResponse = await _httpClient.PostAsync("", content, cancellationToken);

        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);



        _logger.LogDebug("GraphQL response: {Response}", responseBody);



        if (!httpResponse.IsSuccessStatusCode)

        {

            _logger.LogError("GraphQL query failed with status {StatusCode}: {Response}",

                httpResponse.StatusCode, responseBody);

            httpResponse.EnsureSuccessStatusCode();

        }



        using (var document = JsonDocument.Parse(responseBody))

        {

            LogGraphQlComplexity(document);



            if (document.RootElement.TryGetProperty("errors", out var errorsElement) &&

                errorsElement.ValueKind == JsonValueKind.Array &&

                errorsElement.GetArrayLength() > 0)

            {

                var firstError = errorsElement[0];

                var message = firstError.TryGetProperty("message", out var messageElement)

                    ? messageElement.GetString() ?? "GraphQL error"

                    : "GraphQL error";



                if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))

                {

                    throw new ResourceNotFoundException(message);

                }



                throw new InvalidOperationException($"GraphQL error: {message}");

            }

        }



        return JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions

        {

            PropertyNameCaseInsensitive = true

        });

    }



    private void LogGraphQlComplexity(JsonDocument document)

    {

        if (!document.RootElement.TryGetProperty("extensions", out var extensions) || extensions.ValueKind != JsonValueKind.Object)

        {

            return;

        }



        if (!extensions.TryGetProperty("complexity", out var complexity) || complexity.ValueKind != JsonValueKind.Object)

        {

            return;

        }



        var total = TryGetDouble(complexity, "total");

        var remaining = TryGetDouble(complexity, "remaining");

        var queryCost = TryGetDouble(complexity, "query");

        var after = TryGetDouble(complexity, "after");



        if (total.HasValue || remaining.HasValue || queryCost.HasValue || after.HasValue)

        {

            _logger.LogDebug("GraphQL complexity: total={Total}, remaining={Remaining}, query={QueryCost}, after={After}", total, remaining, queryCost, after);

        }

        else

        {

            _logger.LogDebug("GraphQL complexity payload: {Payload}", complexity.GetRawText());

        }

    }



    private static double? TryGetDouble(JsonElement element, string propertyName)

    {

        if (!element.TryGetProperty(propertyName, out var property))

        {

            return null;

        }



        return property.ValueKind switch

        {

            JsonValueKind.Number => property.GetDouble(),

            JsonValueKind.String when double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,

            _ => null

        };

    }



    private string BuildGetBoardItemsQuery(
        string boardId,
        MondayFilterDefinition? filter,
        MondayQueryParams? queryParams)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter?.GroupId))
        {
            arguments.Add($"groups: [\"{filter!.GroupId}\"]");
        }

        if (queryParams is not null && queryParams.HasRules)
        {
            arguments.Add($"query_params: {BuildQueryParamsFragment(queryParams)}");
        }

        var itemsPageArguments = arguments.Count > 0
            ? $"({string.Join(", ", arguments)})"
            : string.Empty;

        return $@"
        {{
            boards(ids: [{boardId}]) {{
                items_page{itemsPageArguments} {{
                    items {{
                        id
                        name
                        group {{ id }}
                        created_at
                        updated_at
                        parent_item {{ id }}
                        column_values {{
                            id
                            value
                            text
                        }}
                    }}
                }}
            }}
        }}";
    }

    private string BuildGetBoardActivityQuery(string boardId, DateTime? fromDate, DateTime? toDate)
    {
        var dateFilter = "";
        if (fromDate.HasValue || toDate.HasValue)
        {
            var from = fromDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "1970-01-01T00:00:00Z";
            var to = toDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            dateFilter = $", from: \"{from}\", to: \"{to}\"";
        }

        return $@"
        {{
            boards(ids: [{boardId}]) {{
                activity_logs{dateFilter} {{
                    event
                    user_id
                    created_at
                    data
                }}
            }}
        }}";
    }

    private string BuildGetBoardUpdatesQuery(string boardId, DateTime? fromDate, DateTime? toDate)
    {
        return $@"
        {{
            boards(ids: [{boardId}]) {{
                items_page {{
                    items {{
                        id
                        updates {{
                            id
                            body
                            creator_id
                            created_at
                        }}
                    }}
                }}
            }}
        }}";
    }

    private string BuildGetSubItemsQuery(string parentItemId)
    {
        return $@"
        {{
            items(ids: [{parentItemId}]) {{
                subitems {{
                    id
                    name
                    group {{ id }}
                    created_at
                    updated_at
                    parent_item {{ id }}
                    column_values {{
                        id
                        value
                        text
                    }}
                }}
            }}
        }}";
    }

    private string BuildGetItemUpdatesQuery(string itemId, DateTime? fromDate, DateTime? toDate)
    {
        return $@"
        {{
            items(ids: [{itemId}]) {{
                updates {{
                    id
                    body
                    creator_id
                    created_at
                }}
            }}
        }}";
    }

    private string BuildUpdateColumnValueMutation(string boardId, string itemId, string columnId, string valueJson)
    {
        // Escape the valueJson for GraphQL
        var escapedValue = valueJson.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return $@"
        mutation {{
            change_column_value(
                board_id: {boardId},
                item_id: {itemId},
                column_id: ""{columnId}"",
                value: ""{escapedValue}""
            ) {{
                id
                name
                group {{ id }}
                created_at
                updated_at
                parent_item {{ id }}
                column_values {{
                    id
                    value
                    text
                }}
            }}
        }}";
    }

    private MondayItemDto MapToMondayItemDto(dynamic item)
    {
        JsonElement jsonItem = (JsonElement)item;
        var columnValues = new Dictionary<string, MondayColumnValueDto>();

        if (jsonItem.TryGetProperty("column_values", out var columnValuesElement) &&
            columnValuesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var col in columnValuesElement.EnumerateArray())
            {
                string colId = col.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                string? colValue = col.TryGetProperty("value", out var valueProp) ? valueProp.GetString() : null;
                string? colText = col.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;

                columnValues[colId] = new MondayColumnValueDto
                {
                    Id = colId,
                    Value = colValue,
                    Text = colText
                };
            }
        }

        string? parentId = null;
        if (jsonItem.TryGetProperty("parent_item", out var parentElement) &&
            parentElement.ValueKind != JsonValueKind.Null &&
            parentElement.TryGetProperty("id", out var parentIdProp))
        {
            parentId = parentIdProp.GetString();
        }

        return new MondayItemDto
        {
            Id = jsonItem.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            ParentId = parentId,
            Title = jsonItem.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            GroupId = jsonItem.TryGetProperty("group", out var group) && group.TryGetProperty("id", out var groupId) ? groupId.GetString() ?? "" : "",
            CreatedAt = jsonItem.TryGetProperty("created_at", out var created) ? ParseDateTimeOffset(created.GetString()) : DateTimeOffset.MinValue,
            UpdatedAt = jsonItem.TryGetProperty("updated_at", out var updated) ? ParseDateTimeOffset(updated.GetString()) : DateTimeOffset.MinValue,
            ColumnValues = columnValues
        };
    }

    private MondayActivityLogDto MapToMondayActivityLogDto(dynamic log)
    {
        JsonElement jsonLog = (JsonElement)log;

        var dataString = jsonLog.TryGetProperty("data", out var data) ? data.GetString() ?? "{}" : "{}";

        return new MondayActivityLogDto
        {
            EventType = jsonLog.TryGetProperty("event", out var evt) ? evt.GetString() ?? "" : "",
            UserId = jsonLog.TryGetProperty("user_id", out var userId) ? userId.GetString() ?? "" : "",
            ItemId = TryExtractActivityItemId(dataString),
            CreatedAt = jsonLog.TryGetProperty("created_at", out var createdAt) ? ParseDateTimeOffset(createdAt.GetString()) : DateTimeOffset.UtcNow,
            EventDataJson = dataString
        };
    }

    private MondayUpdateDto MapToMondayUpdateDto(dynamic update, string itemId)
    {
        JsonElement jsonUpdate = (JsonElement)update;

        return new MondayUpdateDto
        {
            Id = jsonUpdate.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            ItemId = itemId,
            BodyText = jsonUpdate.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
            CreatorId = jsonUpdate.TryGetProperty("creator_id", out var creatorId) ? creatorId.GetString() ?? "" : "",
            CreatedAt = jsonUpdate.TryGetProperty("created_at", out var createdAt) ? ParseDateTimeOffset(createdAt.GetString()) : DateTimeOffset.UtcNow
        };
    }

    private DateTimeOffset ParseDateTimeOffset(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return DateTimeOffset.UtcNow;

        return DateTimeOffset.TryParse(dateString, out var result) ? result : DateTimeOffset.UtcNow;
    }

    private static string BuildQueryParamsFragment(MondayQueryParams queryParams)
    {
        var builder = new StringBuilder();
        builder.Append("{ rules: [");

        for (var index = 0; index < queryParams.Rules.Count; index++)
        {
            var rule = queryParams.Rules[index];
            builder.Append("{ ");
            builder.Append($"column_id: \"{EscapeGraphQlString(rule.ColumnId)}\", ");
            builder.Append($"operator: {rule.Operator}");

            if (rule.RequiresCompareValue && !string.IsNullOrWhiteSpace(rule.CompareValue))
            {
                builder.Append($", compare_value: \"{EscapeGraphQlString(rule.CompareValue!)}\"");
            }

            builder.Append(" }");
            if (index < queryParams.Rules.Count - 1)
            {
                builder.Append(", ");
            }
        }

        builder.Append("] }");
        return builder.ToString();
    }

    private static string EscapeGraphQlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    // Response classes for GraphQL deserialization
    private class BoardColumnsResponse
    {
        public BoardColumnsData? Data { get; set; }
    }

    private class BoardColumnsData
    {
        public List<BoardWithColumns> Boards { get; set; } = new();
    }

    private class BoardWithColumns
    {
        public List<dynamic> Columns { get; set; } = new();
    }

    public async Task<IReadOnlyList<ColumnMetadata>> GetBoardColumnsAsync(
        string boardId,
        CancellationToken cancellationToken)
    {
        var query = BuildGetBoardColumnsQuery(boardId);
        var response = await ExecuteGraphQLQueryAsync<BoardColumnsResponse>(query, cancellationToken);

        if (response?.Data?.Boards == null || !response.Data.Boards.Any())
        {
            throw new ResourceNotFoundException("Board", boardId);
        }

        var columns = response.Data.Boards.First().Columns;
        if (columns == null || !columns.Any())
        {
            return new List<ColumnMetadata>();
        }

        return columns.Select(MapToColumnMetadata).ToList();
    }

    private class BoardItemsResponse
    {
        public BoardItemsData? Data { get; set; }
    }

    private class BoardItemsData
    {
        public List<BoardWithItems> Boards { get; set; } = new();
    }

    private class BoardWithItems
    {
        [JsonPropertyName("items_page")]
        public ItemsPageWithItems? ItemsPage { get; set; }
    }

    private class ItemsPageWithItems
    {
        public List<dynamic> Items { get; set; } = new();
    }

    private class BoardActivityResponse
    {
        public BoardActivityData? Data { get; set; }
    }

    private class BoardActivityData
    {
        public List<BoardWithActivityLogs> Boards { get; set; } = new();
    }

    private class BoardWithActivityLogs
    {
        [JsonPropertyName("activity_logs")]
        public List<dynamic> ActivityLogs { get; set; } = new();
    }

    private class ItemsPageWithUpdates
    {
        public List<ItemWithUpdates> Items { get; set; } = new();
    }

    private class BoardUpdatesResponse
    {
        public BoardUpdatesData? Data { get; set; }
    }

    private class BoardUpdatesData
    {
        public List<BoardWithUpdates> Boards { get; set; } = new();
    }

    private class BoardWithUpdates
    {
        [JsonPropertyName("items_page")]
        public ItemsPageWithUpdates? ItemsPage { get; set; }
    }

    private class ItemWithUpdates
    {
        public string Id { get; set; } = string.Empty;
        public List<dynamic> Updates { get; set; } = new();
    }

    private class ItemSubItemsResponse
    {
        public ItemSubItemsData? Data { get; set; }
    }

    private class ItemSubItemsData
    {
        public List<ItemWithSubItems> Items { get; set; } = new();
    }

    private class ItemWithSubItems
    {
        public List<dynamic> SubItems { get; set; } = new();
    }

    private class ItemUpdatesResponse
    {
        public ItemUpdatesData? Data { get; set; }
    }

    private class ItemUpdatesData
    {
        public List<ItemWithUpdates> Items { get; set; } = new();
    }

    private class UpdateColumnValueResponse
    {
        public UpdateColumnValueData? Data { get; set; }
    }

    private class UpdateColumnValueData
    {
        [JsonPropertyName("change_column_value")]
        public dynamic? ChangeColumnValue { get; set; }
    }

    private string BuildGetBoardColumnsQuery(string boardId)
    {
        return $@"
        {{
            boards(ids: [{boardId}]) {{
                columns {{
                    id
                    title
                    type
                }}
            }}
        }}";
    }

    private ColumnMetadata MapToColumnMetadata(dynamic column)
    {
        JsonElement jsonColumn = (JsonElement)column;

        return new ColumnMetadata
        {
            Id = jsonColumn.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
            Title = jsonColumn.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "",
            Type = jsonColumn.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : ""
        };
    }
    private static string? TryExtractActivityItemId(string? data)
    {
        if (string.IsNullOrWhiteSpace(data) || data == "{}")
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (TryGetString(root, "item_id", out var itemId) ||
                TryGetString(root, "itemId", out itemId) ||
                TryGetString(root, "pulseId", out itemId) ||
                TryGetString(root, "entity_id", out itemId) ||
                TryGetString(root, "entityId", out itemId))
            {
                return itemId;
            }

            if (root.TryGetProperty("entity", out var entity) &&
                (TryGetString(entity, "id", out itemId) ||
                 TryGetString(entity, "item_id", out itemId)))
            {
                return itemId;
            }

            if (root.TryGetProperty("item", out var itemElement) &&
                TryGetString(itemElement, "id", out itemId))
            {
                return itemId;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed data payloads
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            switch (property.ValueKind)
            {
                case JsonValueKind.String:
                    value = property.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                case JsonValueKind.Number when property.TryGetInt64(out var number):
                    value = number.ToString();
                    return true;
            }
        }

        value = null;
        return false;
    }

}





