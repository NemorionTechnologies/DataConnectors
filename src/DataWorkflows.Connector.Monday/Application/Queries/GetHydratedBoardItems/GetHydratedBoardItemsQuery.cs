using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetHydratedBoardItems;

public record GetHydratedBoardItemsQuery(
    string BoardId,
    MondayFilterDefinition? Filter) : IRequest<IEnumerable<MondayHydratedItemDto>>;
