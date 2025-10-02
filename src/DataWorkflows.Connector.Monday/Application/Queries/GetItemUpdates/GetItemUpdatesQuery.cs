using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetItemUpdates;

public record GetItemUpdatesQuery(
    string ItemId,
    DateTime? FromDate,
    DateTime? ToDate) : IRequest<IEnumerable<MondayUpdateDto>>;
