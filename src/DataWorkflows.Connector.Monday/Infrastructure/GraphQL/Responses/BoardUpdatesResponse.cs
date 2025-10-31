using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class BoardUpdatesResponse
{
    public BoardUpdatesData? Data { get; set; }
}

internal class BoardUpdatesData
{
    public List<BoardWithUpdates> Boards { get; set; } = new();
}

internal class BoardWithUpdates
{
    [JsonPropertyName("items_page")]
    public ItemsPageWithUpdates? ItemsPage { get; set; }
}

internal class ItemsPageWithUpdates
{
    public List<ItemWithUpdates> Items { get; set; } = new();
}
