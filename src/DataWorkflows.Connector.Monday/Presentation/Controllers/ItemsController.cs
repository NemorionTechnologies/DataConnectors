using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.Monday.Presentation.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(IMondayApiClient mondayApiClient, ILogger<ItemsController> logger)
    {
        _mondayApiClient = mondayApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Get sub-items for a parent item with optional filtering
    /// </summary>
    [HttpGet("{itemId}/subitems")]
    public async Task<ActionResult<IEnumerable<MondayItemDto>>> GetSubItemsAsync(
        string itemId,
        [FromQuery] GetItemsFilterModel? filter,
        CancellationToken cancellationToken)
    {
        var filterDefinition = GetItemsFilterModel.ToFilterDefinition(filter);
        var result = await _mondayApiClient.GetSubItemsAsync(itemId, filterDefinition, cancellationToken);
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
        var result = await _mondayApiClient.GetItemUpdatesAsync(itemId, fromDate, toDate, cancellationToken);
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
        var result = await _mondayApiClient.UpdateColumnValueAsync(
            request.BoardId,
            itemId,
            columnId,
            request.ValueJson,
            cancellationToken);
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
        // Determine the column ID to use
        var columnId = request.ColumnId ?? request.ColumnTitle ?? throw new ArgumentException("Either ColumnId or ColumnTitle must be provided");

        var result = await _mondayApiClient.UpdateColumnValueAsync(
            request.BoardId,
            itemId,
            columnId,
            request.ValueJson,
            cancellationToken);
        return Ok(result);
    }
}

