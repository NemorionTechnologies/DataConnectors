namespace DataWorkflows.Connector.Monday.Actions.Models;

public class GetItemUpdatesOutput
{
    public List<MondayUpdate> Updates { get; set; } = new();
    public int Count { get; set; }
    public string ItemId { get; set; } = string.Empty;
}

public class MondayUpdate
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
