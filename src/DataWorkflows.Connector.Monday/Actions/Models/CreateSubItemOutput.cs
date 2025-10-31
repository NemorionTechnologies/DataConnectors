namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output for creating a new sub-item under a parent item in Monday.com.
/// </summary>
public class CreateSubItemOutput
{
    /// <summary>
    /// The newly created sub-item with all its properties and column values.
    /// The ParentId property will be populated with the parent item ID.
    /// </summary>
    public required MondayItem Item { get; set; }

    /// <summary>
    /// The ID of the parent item under which the sub-item was created.
    /// </summary>
    public string ParentItemId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the newly created sub-item.
    /// </summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// The board ID of the parent item (sub-items belong to the same board).
    /// </summary>
    public string? BoardId { get; set; }
}
