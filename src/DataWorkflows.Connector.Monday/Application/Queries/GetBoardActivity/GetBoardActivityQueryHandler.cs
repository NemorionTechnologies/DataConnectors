using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardActivity;

public class GetBoardActivityQueryHandler : IRequestHandler<GetBoardActivityQuery, IEnumerable<MondayActivityLogDto>>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<GetBoardActivityQueryHandler> _logger;

    public GetBoardActivityQueryHandler(
        IMondayApiClient mondayApiClient,
        ILogger<GetBoardActivityQueryHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MondayActivityLogDto>> Handle(
        GetBoardActivityQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting activity log for board {BoardId}", request.BoardId);

        var activityLogs = await _mondayApiClient.GetBoardActivityAsync(
            request.BoardId,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} activity logs from board {BoardId}",
            activityLogs.Count(), request.BoardId);

        return activityLogs;
    }
}
