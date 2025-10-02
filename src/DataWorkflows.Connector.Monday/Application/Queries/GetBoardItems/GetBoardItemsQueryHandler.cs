using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetBoardItems;

public class GetBoardItemsQueryHandler : IRequestHandler<GetBoardItemsQuery, IEnumerable<MondayItemDto>>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<GetBoardItemsQueryHandler> _logger;

    public GetBoardItemsQueryHandler(
        IMondayApiClient mondayApiClient,
        ILogger<GetBoardItemsQueryHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MondayItemDto>> Handle(
        GetBoardItemsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting items for board {BoardId}", request.BoardId);

        var items = await _mondayApiClient.GetBoardItemsAsync(
            request.BoardId,
            request.Filter,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} items from board {BoardId}",
            items.Count(), request.BoardId);

        return items;
    }
}
