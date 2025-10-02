using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Queries.GetSubItems;

public class GetSubItemsQueryHandler : IRequestHandler<GetSubItemsQuery, IEnumerable<MondayItemDto>>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<GetSubItemsQueryHandler> _logger;

    public GetSubItemsQueryHandler(
        IMondayApiClient mondayApiClient,
        ILogger<GetSubItemsQueryHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MondayItemDto>> Handle(
        GetSubItemsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting sub-items for parent item {ParentItemId}", request.ParentItemId);

        var subItems = await _mondayApiClient.GetSubItemsAsync(
            request.ParentItemId,
            request.Filter,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} sub-items for parent item {ParentItemId}",
            subItems.Count(), request.ParentItemId);

        return subItems;
    }
}
