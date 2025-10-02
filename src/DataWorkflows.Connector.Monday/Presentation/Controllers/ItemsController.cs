using DataWorkflows.Connector.Monday.Application.Commands.UpdateColumnValue;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Queries.GetItemUpdates;
using DataWorkflows.Connector.Monday.Application.Queries.GetSubItems;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.Monday.Presentation.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(IMediator mediator, ILogger<ItemsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get sub-items for a parent item with optional filtering
    /// </summary>
    [HttpGet("{itemId}/subitems")]
    public async Task<ActionResult<IEnumerable<MondayItemDto>>> GetSubItemsAsync(
        string itemId,
        [FromQuery] GetItemsFilterModel filter,
        CancellationToken cancellationToken)
    {
        var query = new GetSubItemsQuery(itemId, filter ?? new GetItemsFilterModel());
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get updates for a specific item with optional date filtering
    /// </summary>
    [HttpGet("{itemId}/updates")]
    public async Task<ActionResult<IEnumerable<MondayUpdateDto>>> GetItemUpdatesAsync(
        string itemId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = new GetItemUpdatesQuery(itemId, fromDate, toDate);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update a column value for an item or sub-item
    /// </summary>
    /// <remarks>
    /// Backward compatible endpoint - uses columnId from route parameter.
    /// For column title support, use the POST endpoint with ColumnTitle in the request body.
    /// </remarks>
    [HttpPatch("{itemId}/columns/{columnId}")]
    public async Task<ActionResult<MondayItemDto>> UpdateItemColumnValueAsync(
        string itemId,
        string columnId,
        [FromBody] UpdateColumnValueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateColumnValueCommand(
            request.BoardId,
            itemId,
            request.ValueJson,
            ColumnId: columnId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update a column value for an item or sub-item using column ID or title
    /// </summary>
    /// <remarks>
    /// This endpoint supports both ColumnId and ColumnTitle in the request body.
    /// ColumnId takes precedence if both are provided.
    /// Either ColumnId or ColumnTitle must be provided.
    /// </remarks>
    [HttpPost("{itemId}/columns/update")]
    public async Task<ActionResult<MondayItemDto>> UpdateItemColumnAsync(
        string itemId,
        [FromBody] UpdateColumnValueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateColumnValueCommand(
            request.BoardId,
            itemId,
            request.ValueJson,
            request.ColumnId,
            request.ColumnTitle);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
