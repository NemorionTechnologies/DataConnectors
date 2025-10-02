using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardUpdates;

public record GetBoardUpdatesQuery(
    string BoardId,
    DateTime? FromDate,
    DateTime? ToDate) : IRequest<IEnumerable<MondayUpdateDto>>;
