using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class CreateSubItemResponse
{
    public CreateSubItemData? Data { get; set; }
}

internal class CreateSubItemData
{
    [JsonPropertyName("create_subitem")]
    public dynamic? CreateSubItem { get; set; }
}
