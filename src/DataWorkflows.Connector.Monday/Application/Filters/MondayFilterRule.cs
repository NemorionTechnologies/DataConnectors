namespace DataWorkflows.Connector.Monday.Application.Filters;

/// <summary>
/// Represents a single column comparison in a Monday filter definition.
/// </summary>
public sealed record MondayFilterRule(
    string ColumnId,
    string Operator,
    string? Value,
    string? SecondValue = null,
    string? ValueType = null);

public static class MondayFilterValueTypes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Date = "date";
    public const string Timeline = "timeline";
}
