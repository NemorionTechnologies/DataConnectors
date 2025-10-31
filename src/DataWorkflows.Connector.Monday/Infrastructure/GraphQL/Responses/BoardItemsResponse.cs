using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class BoardItemsResponse
{
    public BoardItemsData? Data { get; set; }
}

internal class BoardItemsData
{
    public List<BoardWithItems> Boards { get; set; } = new();
}

internal class BoardWithItems
{
    [JsonPropertyName("items_page")]
    public ItemsPageWithItems? ItemsPage { get; set; }
}

internal class ItemsPageWithItems
{
    public List<dynamic> Items { get; set; } = new();
}
