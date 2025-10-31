using System.Collections.Generic;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class ItemUpdatesResponse
{
    public ItemUpdatesData? Data { get; set; }
}

internal class ItemUpdatesData
{
    public List<ItemWithUpdates> Items { get; set; } = new();
}

internal class ItemWithUpdates
{
    public string Id { get; set; } = string.Empty;
    public List<dynamic> Updates { get; set; } = new();
}
