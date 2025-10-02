using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardItems;

public record GetBoardItemsQuery(
    string BoardId,
    GetItemsFilterModel Filter) : IRequest<IEnumerable<MondayItemDto>>;
