using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetHydratedBoardItems;

public record GetHydratedBoardItemsQuery(
    string BoardId,
    GetItemsFilterModel Filter) : IRequest<IEnumerable<MondayHydratedItemDto>>;
