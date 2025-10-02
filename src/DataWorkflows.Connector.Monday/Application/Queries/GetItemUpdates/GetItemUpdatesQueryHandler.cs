using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetItemUpdates;

public class GetItemUpdatesQueryHandler : IRequestHandler<GetItemUpdatesQuery, IEnumerable<MondayUpdateDto>>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<GetItemUpdatesQueryHandler> _logger;

    public GetItemUpdatesQueryHandler(
        IMondayApiClient mondayApiClient,
        ILogger<GetItemUpdatesQueryHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MondayUpdateDto>> Handle(
        GetItemUpdatesQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting updates for item {ItemId}", request.ItemId);

        var updates = await _mondayApiClient.GetItemUpdatesAsync(
            request.ItemId,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} updates for item {ItemId}",
            updates.Count(), request.ItemId);

        return updates;
    }
}
