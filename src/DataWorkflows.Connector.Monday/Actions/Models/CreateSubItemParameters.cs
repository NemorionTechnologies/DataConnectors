namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for creating a new sub-item under a parent item in Monday.com.
/// </summary>
public class CreateSubItemParameters
{
    /// <summary>
    /// The ID of the parent item under which the sub-item will be created.
    /// </summary>
    public string ParentItemId { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the new sub-item.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Optional column values to set when creating the sub-item.
    /// Format: Dictionary where key is column ID and value is the column value object.
    /// Example: { "status": { "label": "Working on it" }, "date4": "2023-05-25" }
    /// If not provided, the sub-item will copy column values from the parent item.
    /// </summary>
    public Dictionary<string, object>? ColumnValues { get; set; }
}
