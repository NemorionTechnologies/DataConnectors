using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

/// <summary>
/// Service to parse Monday.com column values from their JSON format into strongly-typed objects.
/// </summary>
public interface IColumnValueParser
{
    /// <summary>
    /// Parses a timeline/daterange column value.
    /// </summary>
    /// <param name="columnValue">The column value DTO containing the JSON value</param>
    /// <returns>Parsed timeline value, or null if parsing fails</returns>
    TimelineValue? ParseTimeline(MondayColumnValueDto? columnValue);

    /// <summary>
    /// Gets the text value from a column, preferring the Text property over Value.
    /// </summary>
    /// <param name="columnValue">The column value DTO</param>
    /// <param name="defaultValue">Default value if column is null or empty</param>
    /// <returns>The display text for the column</returns>
    string GetTextValue(MondayColumnValueDto? columnValue, string defaultValue = "");
}
