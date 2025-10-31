namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output for creating a new item on a Monday.com board.
/// </summary>
public class CreateItemOutput
{
    /// <summary>
    /// The newly created item with all its properties and column values.
    /// </summary>
    public required MondayItem Item { get; set; }

    /// <summary>
    /// The board ID where the item was created.
    /// </summary>
    public string BoardId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the newly created item.
    /// </summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// The group ID where the item was created.
    /// </summary>
    public string? GroupId { get; set; }
}
