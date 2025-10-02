using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure;

/// <summary>
/// Parses Monday.com column values from JSON format into strongly-typed objects.
/// </summary>
public class ColumnValueParser : IColumnValueParser
{
    private readonly ILogger<ColumnValueParser> _logger;

    public ColumnValueParser(ILogger<ColumnValueParser> logger)
    {
        _logger = logger;
    }

    public TimelineValue? ParseTimeline(MondayColumnValueDto? columnValue)
    {
        if (columnValue?.Value == null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(columnValue.Value);

            var timeline = new TimelineValue();

            if (json.TryGetProperty("from", out var fromElement))
            {
                var fromStr = fromElement.GetString();
                if (DateTime.TryParse(fromStr, out var fromDate))
                {
                    timeline.From = fromDate;
                }
            }

            if (json.TryGetProperty("to", out var toElement))
            {
                var toStr = toElement.GetString();
                if (DateTime.TryParse(toStr, out var toDate))
                {
                    timeline.To = toDate;
                }
            }

            if (json.TryGetProperty("changed_at", out var changedAtElement))
            {
                var changedAtStr = changedAtElement.GetString();
                if (DateTimeOffset.TryParse(changedAtStr, out var changedAt))
                {
                    timeline.ChangedAt = changedAt;
                }
            }

            return timeline;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse timeline column value: {Value}", columnValue.Value);
            return null;
        }
    }

    public string GetTextValue(MondayColumnValueDto? columnValue, string defaultValue = "")
    {
        if (columnValue == null)
        {
            return defaultValue;
        }

        return !string.IsNullOrWhiteSpace(columnValue.Text)
            ? columnValue.Text
            : columnValue.Value ?? defaultValue;
    }
}
