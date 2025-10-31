using System;
using System.Collections.Generic;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Infrastructure.Parsing;

namespace DataWorkflows.Connector.Monday.Infrastructure.Mapping;

internal class MondayResponseMapper
{
    private readonly ActivityLogParser _activityLogParser;

    public MondayResponseMapper(ActivityLogParser activityLogParser)
    {
        _activityLogParser = activityLogParser;
    }

    public MondayItemDto MapToMondayItemDto(JsonElement item)
    {
        var columnValues = new Dictionary<string, MondayColumnValueDto>();

        if (item.TryGetProperty("column_values", out var columnValuesElement) &&
            columnValuesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var col in columnValuesElement.EnumerateArray())
            {
                var columnId = col.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                var value = col.TryGetProperty("value", out var valueProp) ? valueProp.GetString() : null;
                var text = col.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;

                columnValues[columnId] = new MondayColumnValueDto
                {
                    Id = columnId,
                    Value = value,
                    Text = text
                };
            }
        }

        string? parentId = null;
        if (item.TryGetProperty("parent_item", out var parentElement) &&
            parentElement.ValueKind != JsonValueKind.Null &&
            parentElement.TryGetProperty("id", out var parentIdProp))
        {
            parentId = parentIdProp.GetString();
        }

        return new MondayItemDto
        {
            Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            ParentId = parentId,
            Title = item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            GroupId = item.TryGetProperty("group", out var group) && group.TryGetProperty("id", out var groupId)
                ? groupId.GetString() ?? string.Empty
                : string.Empty,
            CreatedAt = item.TryGetProperty("created_at", out var createdAt)
                ? ParseDateTimeOffset(createdAt.GetString())
                : DateTimeOffset.MinValue,
            UpdatedAt = item.TryGetProperty("updated_at", out var updatedAt)
                ? ParseDateTimeOffset(updatedAt.GetString())
                : DateTimeOffset.MinValue,
            ColumnValues = columnValues
        };
    }

    public MondayActivityLogDto MapToMondayActivityLogDto(JsonElement log)
    {
        var dataString = log.TryGetProperty("data", out var data) ? data.GetString() ?? "{}" : "{}";

        return new MondayActivityLogDto
        {
            EventType = log.TryGetProperty("event", out var evt) ? evt.GetString() ?? string.Empty : string.Empty,
            UserId = log.TryGetProperty("user_id", out var userId) ? userId.GetString() ?? string.Empty : string.Empty,
            ItemId = _activityLogParser.TryExtractActivityItemId(dataString),
            CreatedAt = log.TryGetProperty("created_at", out var createdAt)
                ? ParseDateTimeOffset(createdAt.GetString())
                : DateTimeOffset.UtcNow,
            EventDataJson = dataString
        };
    }

    public MondayUpdateDto MapToMondayUpdateDto(JsonElement update, string itemId)
    {
        return new MondayUpdateDto
        {
            Id = update.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            ItemId = itemId,
            BodyText = update.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty,
            CreatorId = update.TryGetProperty("creator_id", out var creatorId) ? creatorId.GetString() ?? string.Empty : string.Empty,
            CreatedAt = update.TryGetProperty("created_at", out var createdAt)
                ? ParseDateTimeOffset(createdAt.GetString())
                : DateTimeOffset.UtcNow
        };
    }

    public ColumnMetadata MapToColumnMetadata(JsonElement column)
    {
        return new ColumnMetadata
        {
            Id = column.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
            Title = column.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty,
            Type = column.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? string.Empty : string.Empty
        };
    }

    private static DateTimeOffset ParseDateTimeOffset(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return DateTimeOffset.UtcNow;
        }

        return DateTimeOffset.TryParse(dateString, out var result) ? result : DateTimeOffset.UtcNow;
    }
}
