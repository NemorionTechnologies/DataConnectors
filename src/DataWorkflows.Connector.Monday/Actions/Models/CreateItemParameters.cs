namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for creating a new item on a Monday.com board.
/// </summary>
public class CreateItemParameters
{
    /// <summary>
    /// The board ID where the item will be created.
    /// </summary>
    public string BoardId { get; set; } = string.Empty;

    /// <summary>
    /// The name/title of the new item.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Optional group ID where the item should be created.
    /// If not provided, the item will be created in the first group.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Optional column values to set when creating the item.
    /// Format: Dictionary where key is column ID and value is the column value object.
    /// Example: { "status": { "label": "Working on it" }, "date4": "2023-05-25" }
    /// </summary>
    public Dictionary<string, object>? ColumnValues { get; set; }
}
