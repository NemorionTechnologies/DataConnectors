using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure.ColumnFilterEvaluators;

/// <summary>
/// Evaluates filter conditions for link columns.
/// </summary>
public class LinkColumnEvaluator : IColumnFilterEvaluator
{
    public IReadOnlyList<string> SupportedColumnTypes => new[] { "link" };

    public bool Evaluate(MondayColumnValueDto? columnValue, FilterCondition condition, object? expectedValue)
    {
        var linkUrl = ParseValue(columnValue) as string;

        return condition switch
        {
            FilterCondition.IsNull => string.IsNullOrWhiteSpace(linkUrl),
            FilterCondition.IsNotNull => !string.IsNullOrWhiteSpace(linkUrl),
            FilterCondition.Equals => string.Equals(linkUrl, expectedValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            FilterCondition.NotEquals => !string.Equals(linkUrl, expectedValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            FilterCondition.Contains => linkUrl?.Contains(expectedValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            _ => throw new NotSupportedException($"Condition {condition} is not supported for link columns")
        };
    }

    public object? ParseValue(MondayColumnValueDto? columnValue)
    {
        if (columnValue?.Value == null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(columnValue.Value);
            if (json.TryGetProperty("url", out var urlElement))
            {
                return urlElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall back to text representation
        }

        return columnValue.Text;
    }
}
