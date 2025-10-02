using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure.ColumnFilterEvaluators;

/// <summary>
/// Evaluates filter conditions for text columns.
/// This is the fallback evaluator for any column type not specifically handled.
/// </summary>
public class TextColumnEvaluator : IColumnFilterEvaluator
{
    public IReadOnlyList<string> SupportedColumnTypes => new[] { "text", "name", "item_id", "long_text" };

    public bool Evaluate(MondayColumnValueDto? columnValue, FilterCondition condition, object? expectedValue)
    {
        var textValue = ParseValue(columnValue) as string;

        return condition switch
        {
            FilterCondition.IsNull => string.IsNullOrWhiteSpace(textValue),
            FilterCondition.IsNotNull => !string.IsNullOrWhiteSpace(textValue),
            FilterCondition.Equals => string.Equals(textValue, expectedValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            FilterCondition.NotEquals => !string.Equals(textValue, expectedValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            FilterCondition.Contains => textValue?.Contains(expectedValue?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false,
            _ => throw new NotSupportedException($"Condition {condition} is not supported for text columns")
        };
    }

    public object? ParseValue(MondayColumnValueDto? columnValue)
    {
        // Prefer text representation, fall back to value
        return !string.IsNullOrWhiteSpace(columnValue?.Text)
            ? columnValue.Text
            : columnValue?.Value;
    }
}
