namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output from the monday.get-board-columns action.
/// This will be written to context.data[nodeId] for use by subsequent workflow nodes.
/// </summary>
public sealed class GetBoardColumnsOutput
{
    /// <summary>
    /// The columns retrieved from the Monday.com board.
    /// </summary>
    public required List<BoardColumn> Columns { get; init; }

    /// <summary>
    /// The number of columns retrieved.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// The board ID that was queried.
    /// </summary>
    public required string BoardId { get; init; }
}

/// <summary>
/// Metadata about a Monday.com board column.
/// </summary>
public sealed class BoardColumn
{
    /// <summary>
    /// The unique identifier for the column.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The display title/name of the column as shown in Monday.com UI.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The type of the column (e.g., "status", "text", "date", "timeline", "link").
    /// </summary>
    public required string Type { get; init; }
}
