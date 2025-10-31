using System.ComponentModel.DataAnnotations;

namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for the monday.get-board-columns action.
/// These will be validated against a JSON Schema and passed from workflows.
/// </summary>
public sealed class GetBoardColumnsParameters
{
    /// <summary>
    /// The Monday.com board ID to retrieve column metadata from.
    /// </summary>
    [Required]
    public required string BoardId { get; init; }
}
