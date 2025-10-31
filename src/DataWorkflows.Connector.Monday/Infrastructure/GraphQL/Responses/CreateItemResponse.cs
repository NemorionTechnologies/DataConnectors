using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class CreateItemResponse
{
    public CreateItemData? Data { get; set; }
}

internal class CreateItemData
{
    [JsonPropertyName("create_item")]
    public dynamic? CreateItem { get; set; }
}
