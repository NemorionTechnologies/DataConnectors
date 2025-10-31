namespace DataWorkflows.Connector.Monday.Actions.Models;

public class UpdateColumnParameters
{
    public string BoardId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string? ColumnId { get; set; }
    public string? ColumnTitle { get; set; }
    public object Value { get; set; } = new();
}
