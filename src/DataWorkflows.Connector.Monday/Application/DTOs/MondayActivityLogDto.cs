namespace DataWorkflows.Connector.Monday.Application.DTOs;

public class MondayActivityLogDto
{
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string EventDataJson { get; set; } = string.Empty;
}
