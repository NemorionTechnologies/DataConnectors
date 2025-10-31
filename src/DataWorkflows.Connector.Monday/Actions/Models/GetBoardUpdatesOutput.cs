namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Output from the monday.get-board-updates action.
/// This will be written to context.data[nodeId] for use by subsequent workflow nodes.
/// </summary>
public sealed class GetBoardUpdatesOutput
{
    /// <summary>
    /// The updates retrieved from the Monday.com board.
    /// </summary>
    public required List<MondayUpdate> Updates { get; init; }

    /// <summary>
    /// The number of updates retrieved.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// The board ID that was queried.
    /// </summary>
    public required string BoardId { get; init; }
}
