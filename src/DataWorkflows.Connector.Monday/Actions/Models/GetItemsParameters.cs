using DataWorkflows.Connector.Monday.Application.Filters;
using System.ComponentModel.DataAnnotations;

namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for the monday.get-items action.
/// These will be validated against a JSON Schema and passed from workflows.
/// </summary>
public sealed class GetItemsParameters
{
    /// <summary>
    /// The Monday.com board ID to retrieve items from.
    /// </summary>
    [Required]
    public required string BoardId { get; init; }

    /// <summary>
    /// Optional filter to apply when retrieving items.
    /// If null, all items from the board will be retrieved.
    /// </summary>
    public MondayFilterDefinition? Filter { get; init; }
}
