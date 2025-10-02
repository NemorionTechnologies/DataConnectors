using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardUpdates;

public class GetBoardUpdatesQueryHandler : IRequestHandler<GetBoardUpdatesQuery, IEnumerable<MondayUpdateDto>>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<GetBoardUpdatesQueryHandler> _logger;

    public GetBoardUpdatesQueryHandler(
        IMondayApiClient mondayApiClient,
        ILogger<GetBoardUpdatesQueryHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MondayUpdateDto>> Handle(
        GetBoardUpdatesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting updates for board {BoardId}", request.BoardId);

        var updates = await _mondayApiClient.GetBoardUpdatesAsync(
            request.BoardId,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} updates from board {BoardId}",
            updates.Count(), request.BoardId);

        return updates;
    }
}
