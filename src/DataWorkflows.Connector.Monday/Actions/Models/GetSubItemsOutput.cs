namespace DataWorkflows.Connector.Monday.Actions.Models;

public class GetSubItemsOutput
{
    public List<MondayItem> Items { get; set; } = new();
    public int Count { get; set; }
    public string ParentItemId { get; set; } = string.Empty;
}
