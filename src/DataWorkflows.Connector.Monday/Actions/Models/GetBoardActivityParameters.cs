namespace DataWorkflows.Connector.Monday.Actions.Models;

public class GetBoardActivityParameters
{
    public string BoardId { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
