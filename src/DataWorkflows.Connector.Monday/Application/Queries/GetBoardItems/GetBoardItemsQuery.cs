using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardItems;

public record GetBoardItemsQuery(
    string BoardId,
    MondayFilterDefinition? Filter) : IRequest<IEnumerable<MondayItemDto>>;
