using System.ComponentModel.DataAnnotations;

namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for the monday.get-board-updates action.
/// These will be validated against a JSON Schema and passed from workflows.
/// </summary>
public sealed class GetBoardUpdatesParameters
{
    /// <summary>
    /// The Monday.com board ID to retrieve updates from.
    /// </summary>
    [Required]
    public required string BoardId { get; init; }

    /// <summary>
    /// Optional start date filter - only updates created after this date will be included.
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// Optional end date filter - only updates created before this date will be included.
    /// </summary>
    public DateTime? ToDate { get; init; }
}
