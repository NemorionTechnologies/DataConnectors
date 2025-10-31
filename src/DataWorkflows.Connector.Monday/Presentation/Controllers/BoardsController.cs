using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.Monday.Presentation.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BoardsController : ControllerBase
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly IColumnMetadataCache _cache;
    private readonly ILogger<BoardsController> _logger;

    public BoardsController(
        IMondayApiClient mondayApiClient,
        IColumnMetadataCache cache,
        ILogger<BoardsController> logger)
    {
        _mondayApiClient = mondayApiClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get all items from a board with optional filtering
    /// </summary>
    [HttpGet("{boardId}/items")]
    public async Task<ActionResult<IEnumerable<MondayItemDto>>> GetItemsByBoardIdAsync(
        string boardId,
        [FromQuery] GetItemsFilterModel? filter,
        CancellationToken cancellationToken)
    {
        var filterDefinition = GetItemsFilterModel.ToFilterDefinition(filter);
        var result = await _mondayApiClient.GetBoardItemsAsync(boardId, filterDefinition, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get activity log for a board with optional date filtering
    /// </summary>
    [HttpGet("{boardId}/activity")]
    public async Task<ActionResult<IEnumerable<MondayActivityLogDto>>> GetBoardActivityLogAsync(
        string boardId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mondayApiClient.GetBoardActivityAsync(boardId, fromDate, toDate, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all updates from a board with optional date filtering
    /// </summary>
    [HttpGet("{boardId}/updates")]
    public async Task<ActionResult<IEnumerable<MondayUpdateDto>>> GetBoardUpdatesAsync(
        string boardId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _mondayApiClient.GetBoardUpdatesAsync(boardId, fromDate, toDate, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get hydrated board items (items with sub-items and updates) with optional filtering
    /// </summary>
    [HttpGet("{boardId}/hydrated-items")]
    public async Task<ActionResult<IEnumerable<MondayHydratedItemDto>>> GetHydratedItemsByBoardIdAsync(
        string boardId,
        [FromQuery] GetItemsFilterModel? filter,
        CancellationToken cancellationToken)
    {
        var filterDefinition = GetItemsFilterModel.ToFilterDefinition(filter);
        var result = await _mondayApiClient.GetHydratedBoardItemsAsync(boardId, filterDefinition, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Flush column metadata cache for a specific board
    /// </summary>
    [HttpDelete("{boardId}/cache/columns")]
    public async Task<ActionResult> FlushBoardColumnCacheAsync(string boardId)
    {
        _logger.LogInformation("Flushing column cache for board {BoardId}", boardId);
        await _cache.FlushBoardAsync(boardId);
        return NoContent();
    }

    /// <summary>
    /// Flush column metadata cache for all boards
    /// </summary>
    [HttpDelete("cache/columns")]
    public async Task<ActionResult> FlushAllColumnCachesAsync()
    {
        _logger.LogInformation("Flushing column cache for all boards");
        await _cache.FlushAllAsync();
        return NoContent();
    }
}

