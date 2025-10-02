using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Commands.UpdateColumnValue;

/// <summary>
/// Command to update a column value on a Monday.com item.
/// Either ColumnId or ColumnTitle must be provided. ColumnId takes precedence if both are provided.
/// </summary>
public record UpdateColumnValueCommand(
    string BoardId,
    string ItemId,
    string ValueJson,
    string? ColumnId = null,
    string? ColumnTitle = null) : IRequest<MondayItemDto>;
