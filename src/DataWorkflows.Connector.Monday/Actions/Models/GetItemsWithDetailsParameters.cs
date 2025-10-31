using DataWorkflows.Connector.Monday.Application.Filters;
using System.ComponentModel.DataAnnotations;

namespace DataWorkflows.Connector.Monday.Actions.Models;

/// <summary>
/// Parameters for the monday.get-items-with-details action.
/// Retrieves items with their sub-items and updates included in a single call.
/// </summary>
public sealed class GetItemsWithDetailsParameters
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
