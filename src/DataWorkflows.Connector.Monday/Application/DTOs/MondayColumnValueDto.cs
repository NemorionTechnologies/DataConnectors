namespace DataWorkflows.Connector.Monday.Application.DTOs;

/// <summary>
/// Represents a column value from Monday.com with both the raw JSON value and human-readable text.
/// </summary>
public class MondayColumnValueDto
{
    /// <summary>
    /// The column ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The raw JSON value of the column (e.g., {"index":3,"changed_at":"..."})
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// The human-readable text representation of the column value (e.g., "Waiting for review")
    /// This is what users see in the Monday.com UI
    /// </summary>
    public string? Text { get; set; }
}
