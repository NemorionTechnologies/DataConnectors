namespace DataWorkflows.Connector.Monday.Application.DTOs;

public class GetItemsFilterModel
{
    public string? GroupId { get; set; }
    public DateRangeFilter? TimelineFilter { get; set; }
    public Dictionary<string, string>? ColumnFilters { get; set; } // Key: ColumnName, Value: ColumnValue
}
