namespace DataWorkflows.Connector.Monday.Application.DTOs;

public class MondayUpdateDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
