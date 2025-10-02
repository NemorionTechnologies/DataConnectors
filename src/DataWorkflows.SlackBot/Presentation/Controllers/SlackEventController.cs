using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.SlackBot.Presentation.Controllers;

[ApiController]
[Route("api/v1/slack")]
public class SlackEventController : ControllerBase
{
    private readonly ILogger<SlackEventController> _logger;

    public SlackEventController(ILogger<SlackEventController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles Slack slash commands.
    /// </summary>
    [HttpPost("commands")]
    public async Task<IActionResult> HandleSlashCommand([FromForm] SlackSlashCommandDto command)
    {
        _logger.LogInformation("Received Slack slash command: {Command}", command.Command);

        // TODO: Parse command and call Workflow Engine
        await Task.CompletedTask;

        return Ok(new { text = $"Processing command: {command.Text}" });
    }

    /// <summary>
    /// Handles Slack events (mentions, messages, etc.).
    /// </summary>
    [HttpPost("events")]
    public async Task<IActionResult> HandleEvent([FromBody] object eventPayload)
    {
        _logger.LogInformation("Received Slack event");

        // TODO: Process event and call Workflow Engine
        await Task.CompletedTask;

        return Ok();
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "slack-bot", timestamp = DateTime.UtcNow });
    }
}

public record SlackSlashCommandDto(
    string Command,
    string Text,
    string UserId,
    string UserName,
    string ChannelId);
