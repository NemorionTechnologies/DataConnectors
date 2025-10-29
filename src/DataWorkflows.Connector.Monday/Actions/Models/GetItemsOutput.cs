namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output from the monday.get-items action.
/// This will be written to context.data[nodeId] for use by subsequent workflow nodes.
/// </summary>
public sealed class GetItemsOutput
{
    /// <summary>
    /// The items retrieved from the Monday.com board.
    /// </summary>
    public required List<MondayItem> Items { get; init; }

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
/// Simplified representation of a Monday.com item for workflow outputs.
/// </summary>
public sealed class MondayItem
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
}

/// <summary>
/// A column value with both structured data and human-readable text.
/// </summary>
public sealed class ColumnValue
{
    /// <summary>
    /// The raw JSON value from Monday.com.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Human-readable display text for the column value.
    /// </summary>
    public required string Text { get; init; }
}
