using DataWorkflows.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.Monday.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MondayConnectorController : ControllerBase
{
    private readonly ILogger<MondayConnectorController> _logger;

    public MondayConnectorController(ILogger<MondayConnectorController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get items from a Monday.com board.
    /// </summary>
    [HttpGet("boards/{boardId}/items")]
    public async Task<ActionResult<ConnectorResponse<List<ConnectorItemDto>>>> GetBoardItems(string boardId)
    {
        _logger.LogInformation("Fetching items from Monday board: {BoardId}", boardId);

        // TODO: Implement actual Monday.com API call with Polly retry policy
        await Task.Delay(100);

        return Ok(new ConnectorResponse<List<ConnectorItemDto>>
        {
            Success = true,
            Data = new List<ConnectorItemDto>
            {
                new() { Id = "1", Title = "Sample Task", Status = "Working on it" }
            }
        });
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "monday-connector", timestamp = DateTime.UtcNow });
    }
}
