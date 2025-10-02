namespace DataWorkflows.Connector.Monday.Application.DTOs;

/// <summary>
/// Represents a filter condition to apply to a Monday.com column.
/// </summary>
public class ColumnFilter
{
    /// <summary>
    /// The human-readable column title (e.g., "GitHub link", "Status", "Timeline")
    /// </summary>
    public string ColumnTitle { get; set; } = string.Empty;

    /// <summary>
    /// The filter condition to apply
    /// </summary>
    public FilterCondition Condition { get; set; }

    /// <summary>
    /// The expected value to compare against (type depends on column type and condition)
    /// - For IsNull/IsNotNull: can be null
    /// - For Equals/NotEquals/Contains: string, int, DateTime, etc.
    /// - For GreaterThan/LessThan: int, DateTime, etc.
    /// </summary>
    public object? ExpectedValue { get; set; }

    public ColumnFilter() { }

    public ColumnFilter(string columnTitle, FilterCondition condition, object? expectedValue = null)
    {
        ColumnTitle = columnTitle;
        Condition = condition;
        ExpectedValue = expectedValue;
    }
}

/// <summary>
/// Filter conditions that can be applied to column values
/// </summary>
public enum FilterCondition
{
    /// <summary>
    /// Column value is null or empty
    /// </summary>
    IsNull,

    /// <summary>
    /// Column value is not null and not empty
    /// </summary>
    IsNotNull,

    /// <summary>
    /// Column value equals the expected value
    /// </summary>
    Equals,

    /// <summary>
    /// Column value does not equal the expected value
    /// </summary>
    NotEquals,

    /// <summary>
    /// Column text contains the expected string (case-insensitive)
    /// </summary>
    Contains,

    /// <summary>
    /// Column value is greater than the expected value (for numbers, dates)
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Column value is less than the expected value (for numbers, dates)
    /// </summary>
    LessThan,

    /// <summary>
    /// Column value is greater than or equal to the expected value
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Column value is less than or equal to the expected value
    /// </summary>
    LessThanOrEqual
}
