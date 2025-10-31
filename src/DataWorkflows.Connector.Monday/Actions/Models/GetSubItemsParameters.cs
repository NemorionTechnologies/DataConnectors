using DataWorkflows.Connector.Monday.Application.Filters;

namespace DataWorkflows.Connector.Monday.Actions.Models;

public class GetSubItemsParameters
{
    public string ParentItemId { get; set; } = string.Empty;
    public MondayFilterDefinition? Filter { get; set; }
}
