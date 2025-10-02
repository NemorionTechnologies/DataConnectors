using DataWorkflows.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.Outlook.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OutlookConnectorController : ControllerBase
{
    private readonly ILogger<OutlookConnectorController> _logger;

    public OutlookConnectorController(ILogger<OutlookConnectorController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get recent emails from Outlook.
    /// </summary>
    [HttpGet("emails")]
    public async Task<ActionResult<ConnectorResponse<List<ConnectorItemDto>>>> GetEmails([FromQuery] int count = 10)
    {
        _logger.LogInformation("Fetching {Count} recent emails from Outlook", count);

        // TODO: Implement actual Microsoft Graph API call with Polly retry policy
        await Task.Delay(100);

        return Ok(new ConnectorResponse<List<ConnectorItemDto>>
        {
            Success = true,
            Data = new List<ConnectorItemDto>
            {
                new() { Id = "1", Title = "Sample Email", Description = "Email body" }
            }
        });
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "outlook-connector", timestamp = DateTime.UtcNow });
    }
}
