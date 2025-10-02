using DataWorkflows.Connector.Monday.Application.DTOs;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardActivity;

public record GetBoardActivityQuery(
    string BoardId,
    DateTime? FromDate,
    DateTime? ToDate) : IRequest<IEnumerable<MondayActivityLogDto>>;
