using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetHydratedBoardItems;

public class GetHydratedBoardItemsQueryHandler : IRequestHandler<GetHydratedBoardItemsQuery, IEnumerable<MondayHydratedItemDto>>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<GetHydratedBoardItemsQueryHandler> _logger;

    public GetHydratedBoardItemsQueryHandler(
        IMondayApiClient mondayApiClient,
        ILogger<GetHydratedBoardItemsQueryHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MondayHydratedItemDto>> Handle(
        GetHydratedBoardItemsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting hydrated items for board {BoardId}", request.BoardId);

        var hydratedItems = await _mondayApiClient.GetHydratedBoardItemsAsync(
            request.BoardId,
            request.Filter,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} hydrated items from board {BoardId}",
            hydratedItems.Count(), request.BoardId);

        return hydratedItems;
    }
}
