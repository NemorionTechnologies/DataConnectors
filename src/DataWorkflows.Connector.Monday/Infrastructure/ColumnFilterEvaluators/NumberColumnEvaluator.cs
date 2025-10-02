using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure.ColumnFilterEvaluators;

/// <summary>
/// Evaluates filter conditions for number columns.
/// </summary>
public class NumberColumnEvaluator : IColumnFilterEvaluator
{
    public IReadOnlyList<string> SupportedColumnTypes => new[] { "numbers", "numeric" };

    public bool Evaluate(MondayColumnValueDto? columnValue, FilterCondition condition, object? expectedValue)
    {
        var numberValue = ParseValue(columnValue) as double?;

        return condition switch
        {
            FilterCondition.IsNull => numberValue == null,
            FilterCondition.IsNotNull => numberValue != null,
            FilterCondition.Equals => numberValue.HasValue && CompareNumber(numberValue.Value, expectedValue, (n, e) => n == e),
            FilterCondition.NotEquals => numberValue.HasValue && CompareNumber(numberValue.Value, expectedValue, (n, e) => n != e),
            FilterCondition.GreaterThan => numberValue.HasValue && CompareNumber(numberValue.Value, expectedValue, (n, e) => n > e),
            FilterCondition.LessThan => numberValue.HasValue && CompareNumber(numberValue.Value, expectedValue, (n, e) => n < e),
            FilterCondition.GreaterThanOrEqual => numberValue.HasValue && CompareNumber(numberValue.Value, expectedValue, (n, e) => n >= e),
            FilterCondition.LessThanOrEqual => numberValue.HasValue && CompareNumber(numberValue.Value, expectedValue, (n, e) => n <= e),
            _ => throw new NotSupportedException($"Condition {condition} is not supported for number columns")
        };
    }

    public object? ParseValue(MondayColumnValueDto? columnValue)
    {
        if (columnValue?.Value == null)
        {
            return null;
        }

        // Try text first, then value
        var stringValue = !string.IsNullOrWhiteSpace(columnValue.Text)
            ? columnValue.Text.Trim().Trim('"')
            : columnValue.Value.Trim().Trim('"');

        if (double.TryParse(stringValue, out var number))
        {
            return number;
        }

        return null;
    }

    private bool CompareNumber(double numberValue, object? expectedValue, Func<double, double, bool> comparer)
    {
        if (expectedValue == null)
        {
            return false;
        }

        var compareValue = expectedValue switch
        {
            double d => d,
            int i => (double)i,
            long l => (double)l,
            decimal dec => (double)dec,
            float f => (double)f,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => throw new ArgumentException($"Cannot compare number with type {expectedValue.GetType().Name}")
        };

        return comparer(numberValue, compareValue);
    }
}
