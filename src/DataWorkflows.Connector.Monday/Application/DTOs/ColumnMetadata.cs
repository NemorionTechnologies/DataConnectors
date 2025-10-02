namespace DataWorkflows.Connector.Monday.Application.DTOs;

/// <summary>
/// Metadata about a Monday.com board column.
/// Used for caching column information to enable title-to-id resolution.
/// </summary>
public class ColumnMetadata
{
    /// <summary>
    /// The unique identifier for the column
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display title/name of the column as shown in Monday.com UI
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The type of the column (e.g., "status", "text", "date", "timeline", "link")
    /// </summary>
    public string Type { get; set; } = string.Empty;
}
