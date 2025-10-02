using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

/// <summary>
/// Evaluates filter conditions for Monday.com column values.
/// Different implementations handle different column types (link, status, timeline, text, number, etc.)
/// </summary>
public interface IColumnFilterEvaluator
{
    /// <summary>
    /// Column types this evaluator can handle
    /// </summary>
    IReadOnlyList<string> SupportedColumnTypes { get; }

    /// <summary>
    /// Evaluates whether a column value matches the filter condition.
    /// </summary>
    /// <param name="columnValue">The Monday column value to evaluate</param>
    /// <param name="condition">The filter condition</param>
    /// <param name="expectedValue">The expected value to compare against</param>
    /// <returns>True if the column value matches the filter condition</returns>
    bool Evaluate(MondayColumnValueDto? columnValue, FilterCondition condition, object? expectedValue);

    /// <summary>
    /// Parses the column value into a native .NET type for the ParsedColumns dictionary.
    /// </summary>
    /// <param name="columnValue">The Monday column value to parse</param>
    /// <returns>Parsed value as a native .NET type, or null if parsing fails</returns>
    object? ParseValue(MondayColumnValueDto? columnValue);
}
