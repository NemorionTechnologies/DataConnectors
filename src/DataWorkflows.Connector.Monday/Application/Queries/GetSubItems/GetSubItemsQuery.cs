using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetSubItems;

public record GetSubItemsQuery(
    string ParentItemId,
    GetItemsFilterModel Filter) : IRequest<IEnumerable<MondayItemDto>>;
