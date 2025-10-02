using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.TaskTracker.MockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Liveness probe - checks if the service is running.
    /// </summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "healthy", service = "tasktracker-mock-api", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe - checks if the service can accept traffic.
    /// </summary>
    [HttpGet("ready")]
    public IActionResult Ready()
    {
        // Mock API has no external dependencies, so always ready if alive
        return Ok(new
        {
            status = "ready",
            service = "tasktracker-mock-api",
            timestamp = DateTime.UtcNow
        });
    }
}
