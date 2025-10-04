using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetSubItems;

public record GetSubItemsQuery(
    string ParentItemId,
    MondayFilterDefinition? Filter) : IRequest<IEnumerable<MondayItemDto>>;
