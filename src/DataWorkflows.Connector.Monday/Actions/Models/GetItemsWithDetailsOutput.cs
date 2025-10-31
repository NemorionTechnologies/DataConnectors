namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output from the monday.get-items-with-details action.
/// This will be written to context.data[nodeId] for use by subsequent workflow nodes.
/// </summary>
public sealed class GetItemsWithDetailsOutput
{
    /// <summary>
    /// The items retrieved from the Monday.com board, with sub-items and updates included.
    /// </summary>
    public required List<MondayItemWithDetails> Items { get; init; }

    /// <summary>
    /// The number of items retrieved.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// The board ID that was queried.
    /// </summary>
    public required string BoardId { get; init; }
}

/// <summary>
/// A Monday.com item with its sub-items and updates pre-loaded.
/// </summary>
public sealed class MondayItemWithDetails
{
    /// <summary>
    /// The Monday.com item ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The parent item ID (for subitems).
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// The item title/name.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The group/section ID this item belongs to.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// When the item was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the item was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Column values for this item.
    /// Key is the column ID, value contains both JSON value and display text.
    /// </summary>
    public required Dictionary<string, ColumnValue> ColumnValues { get; init; }

    /// <summary>
    /// Sub-items belonging to this item.
    /// </summary>
    public required List<MondayItem> SubItems { get; init; }

    /// <summary>
    /// Updates/comments on this item.
    /// </summary>
    public required List<MondayUpdate> Updates { get; init; }
}
