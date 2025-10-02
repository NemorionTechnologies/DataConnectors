using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using MediatR;

namespace DataWorkflows.Connector.Monday.Application.Commands.UpdateColumnValue;

public class UpdateColumnValueCommandHandler : IRequestHandler<UpdateColumnValueCommand, MondayItemDto>
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly IColumnResolverService _columnResolver;
    private readonly ILogger<UpdateColumnValueCommandHandler> _logger;

    public UpdateColumnValueCommandHandler(
        IMondayApiClient mondayApiClient,
        IColumnResolverService columnResolver,
        ILogger<UpdateColumnValueCommandHandler> logger)
    {
        _mondayApiClient = mondayApiClient;
        _columnResolver = columnResolver;
        _logger = logger;
    }

    public async Task<MondayItemDto> Handle(
        UpdateColumnValueCommand request,
        CancellationToken cancellationToken)
    {
        var resolvedColumnId = await _columnResolver.ResolveColumnIdAsync(
            request.BoardId,
            request.ColumnId,
            request.ColumnTitle,
            cancellationToken);

        _logger.LogInformation(
            "Updating column {ColumnId} for item {ItemId}",
            resolvedColumnId,
            request.ItemId);

        var updatedItem = await _mondayApiClient.UpdateColumnValueAsync(
            request.BoardId,
            request.ItemId,
            resolvedColumnId,
            request.ValueJson,
            cancellationToken);

        _logger.LogInformation(
            "Successfully updated column {ColumnId} for item {ItemId}",
            resolvedColumnId,
            request.ItemId);

        return updatedItem;
    }
}
