namespace DataWorkflows.Connector.Monday.Actions.Models;

public class GetBoardActivityOutput
{
    public List<MondayActivityLog> Activities { get; set; } = new();
    public int Count { get; set; }
    public string BoardId { get; set; } = string.Empty;
}

public class MondayActivityLog
{
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? ItemId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string EventDataJson { get; set; } = string.Empty;
}
