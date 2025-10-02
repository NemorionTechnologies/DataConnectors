namespace DataWorkflows.Connector.Monday.Application.DTOs;

/// <summary>
/// Request to update a column value on a Monday.com item.
/// Either ColumnId or ColumnTitle must be provided. ColumnId takes precedence if both are provided.
/// </summary>
public record UpdateColumnValueRequest(
    string BoardId,
    string ValueJson,
    string? ColumnId = null,
    string? ColumnTitle = null);
