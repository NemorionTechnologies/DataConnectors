namespace DataWorkflows.Connector.Monday.Actions.Models;

public class UpdateColumnOutput
{
    public required MondayItem Item { get; set; }
    public string BoardId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ColumnId { get; set; } = string.Empty;
}
