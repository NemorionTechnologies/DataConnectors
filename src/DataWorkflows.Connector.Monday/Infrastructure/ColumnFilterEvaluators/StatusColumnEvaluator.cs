using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure.ColumnFilterEvaluators;

/// <summary>
/// Evaluates filter conditions for status columns.
/// </summary>
public class StatusColumnEvaluator : IColumnFilterEvaluator
{
    public IReadOnlyList<string> SupportedColumnTypes => new[] { "status" };

    public bool Evaluate(MondayColumnValueDto? columnValue, FilterCondition condition, object? expectedValue)
    {
        var statusText = ParseValue(columnValue) as string;

        return condition switch
        {
            FilterCondition.IsNull => string.IsNullOrWhiteSpace(statusText),
            FilterCondition.IsNotNull => !string.IsNullOrWhiteSpace(statusText),
            FilterCondition.Equals => string.Equals(statusText, expectedValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            FilterCondition.NotEquals => !string.Equals(statusText, expectedValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            FilterCondition.Contains => statusText?.Contains(expectedValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            _ => throw new NotSupportedException($"Condition {condition} is not supported for status columns")
        };
    }

    public object? ParseValue(MondayColumnValueDto? columnValue)
    {
        // Status columns have human-readable text
        return !string.IsNullOrWhiteSpace(columnValue?.Text) ? columnValue.Text : null;
    }
}
