using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DataWorkflows.Engine.Presentation.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IConfiguration configuration, ILogger<HealthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe - checks if the service is running.
    /// </summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "healthy", service = "workflow-engine", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe - checks if the service can accept traffic (all dependencies are available).
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        try
        {
            // Check database connection
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
            }

            return Ok(new
            {
                status = "ready",
                service = "workflow-engine",
                dependencies = new { database = "connected" },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new
            {
                status = "not ready",
                service = "workflow-engine",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
