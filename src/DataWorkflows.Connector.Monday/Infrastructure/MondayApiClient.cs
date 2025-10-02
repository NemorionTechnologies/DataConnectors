using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Domain.Exceptions;

namespace DataWorkflows.Connector.Monday.Infrastructure;

public class MondayApiClient : IMondayApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MondayApiClient> _logger;
    private readonly string _apiKey;

    public MondayApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MondayApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Monday:ApiKey"] ?? throw new InvalidOperationException("Monday API key not configured");

        _httpClient.BaseAddress = new Uri("https://api.monday.com/v2/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("API-Version", "2024-10");
    }

    public async Task<IEnumerable<MondayItemDto>> GetBoardItemsAsync(
        string boardId,
        GetItemsFilterModel filter,
        CancellationToken cancellationToken)
    {
        var query = BuildGetBoardItemsQuery(boardId, filter);
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

        return itemsPage.Items.Select(MapToMondayItemDto);
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
        GetItemsFilterModel filter,
        CancellationToken cancellationToken)
    {
        var query = BuildGetSubItemsQuery(parentItemId);
        var response = await ExecuteGraphQLQueryAsync<ItemSubItemsResponse>(query, cancellationToken);

        if (response?.Data?.Items == null || !response.Data.Items.Any())
        {
            throw new ResourceNotFoundException("Item", parentItemId);
        }

        var subItems = response.Data.Items.First().SubItems.Select(MapToMondayItemDto);

        // Apply in-memory filtering since Monday.com API doesn't support server-side filtering of sub-items
        return ApplyFilterToItems(subItems, filter);
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
        GetItemsFilterModel filter,
        CancellationToken cancellationToken)
    {
        // First, get the parent items
        var parentItems = await GetBoardItemsAsync(boardId, filter, cancellationToken);

        // Then, concurrently fetch sub-items and updates for each parent item
        var hydratedItemsTasks = parentItems.Select(async item =>
        {
            var subItemsTask = GetSubItemsAsync(item.Id, new GetItemsFilterModel(), cancellationToken);
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

        var result = JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Check for GraphQL errors
        var errorCheck = JsonSerializer.Deserialize<GraphQLErrorResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (errorCheck?.Errors != null && errorCheck.Errors.Any())
        {
            var firstError = errorCheck.Errors.First();
            if (firstError.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                throw new ResourceNotFoundException(firstError.Message);
            }
            throw new InvalidOperationException($"GraphQL error: {firstError.Message}");
        }

        return result;
    }

    private string BuildGetBoardItemsQuery(string boardId, GetItemsFilterModel filter)
    {
        var groupFilter = filter.GroupId != null ? $", groups: [\"{filter.GroupId}\"]" : "";

        return $@"
        {{
            boards(ids: [{boardId}]) {{
                items_page{groupFilter} {{
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

        return new MondayActivityLogDto
        {
            EventType = jsonLog.TryGetProperty("event", out var evt) ? evt.GetString() ?? "" : "",
            UserId = jsonLog.TryGetProperty("user_id", out var userId) ? userId.GetString() ?? "" : "",
            CreatedAt = jsonLog.TryGetProperty("created_at", out var createdAt) ? ParseDateTimeOffset(createdAt.GetString()) : DateTimeOffset.UtcNow,
            EventDataJson = jsonLog.TryGetProperty("data", out var data) ? data.GetString() ?? "{}" : "{}"
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

    private IEnumerable<MondayItemDto> ApplyFilterToItems(IEnumerable<MondayItemDto> items, GetItemsFilterModel filter)
    {
        var filtered = items.AsEnumerable();

        if (filter.GroupId != null)
        {
            filtered = filtered.Where(i => i.GroupId == filter.GroupId);
        }

        if (filter.TimelineFilter != null)
        {
            filtered = filtered.Where(i =>
                i.CreatedAt >= filter.TimelineFilter.From &&
                i.CreatedAt <= filter.TimelineFilter.To);
        }

        if (filter.ColumnFilters != null)
        {
            foreach (var columnFilter in filter.ColumnFilters)
            {
                filtered = filtered.Where(i =>
                    i.ColumnValues.ContainsKey(columnFilter.Key) &&
                    i.ColumnValues[columnFilter.Key]?.ToString() == columnFilter.Value);
            }
        }

        return filtered;
    }

    // Response classes for GraphQL deserialization
    private class GraphQLErrorResponse
    {
        public List<GraphQLError>? Errors { get; set; }
    }

    private class GraphQLError
    {
        public string Message { get; set; } = "";
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
        public ItemsPage? ItemsPage { get; set; }
    }

    private class ItemsPage
    {
        public List<dynamic> Items { get; set; } = new();
    }

    private class BoardActivityResponse
    {
        public BoardActivityData? Data { get; set; }
    }

    private class BoardActivityData
    {
        public List<BoardWithActivity> Boards { get; set; } = new();
    }

    private class BoardWithActivity
    {
        public List<dynamic> ActivityLogs { get; set; } = new();
    }

    private class BoardUpdatesResponse
    {
        public BoardUpdatesData? Data { get; set; }
    }

    private class BoardUpdatesData
    {
        public List<BoardWithItemUpdates> Boards { get; set; } = new();
    }

    private class BoardWithItemUpdates
    {
        [JsonPropertyName("items_page")]
        public ItemsPageWithUpdates? ItemsPage { get; set; }
    }

    private class ItemsPageWithUpdates
    {
        public List<ItemWithUpdates> Items { get; set; } = new();
    }

    private class ItemWithUpdates
    {
        public string Id { get; set; } = "";
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
}
