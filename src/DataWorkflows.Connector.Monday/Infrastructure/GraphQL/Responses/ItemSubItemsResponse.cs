using System.Collections.Generic;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class ItemSubItemsResponse
{
    public ItemSubItemsData? Data { get; set; }
}

internal class ItemSubItemsData
{
    public List<ItemWithSubItems> Items { get; set; } = new();
}

internal class ItemWithSubItems
{
    public List<dynamic> SubItems { get; set; } = new();
}
