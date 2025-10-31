namespace DataWorkflows.Connector.Monday.Actions.Models;

public class GetItemUpdatesParameters
{
    public string ItemId { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
