using DataWorkflows.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.Connector.TaskTracker.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TaskTrackerConnectorController : ControllerBase
{
    private readonly ILogger<TaskTrackerConnectorController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public TaskTrackerConnectorController(
        ILogger<TaskTrackerConnectorController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get tasks from the proprietary task tracker.
    /// </summary>
    [HttpGet("tasks")]
    public async Task<ActionResult<ConnectorResponse<List<ConnectorItemDto>>>> GetTasks()
    {
        _logger.LogInformation("Fetching tasks from TaskTracker");

        // TODO: Implement actual call to TaskTracker Mock API with Polly retry policy
        await Task.Delay(100);

        return Ok(new ConnectorResponse<List<ConnectorItemDto>>
        {
            Success = true,
            Data = new List<ConnectorItemDto>
            {
                new() { Id = "1", Title = "Sample Task from TaskTracker", Status = "In Progress" }
            }
        });
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "tasktracker-connector", timestamp = DateTime.UtcNow });
    }
}
