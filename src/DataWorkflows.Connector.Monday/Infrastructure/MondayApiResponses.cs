using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure;

/// <summary>
/// Response classes for Monday.com GraphQL API deserialization.
/// </summary>
internal class CreateItemResponse
{
    public CreateItemData? Data { get; set; }
}

internal class CreateItemData
{
    [JsonPropertyName("create_item")]
    public dynamic? CreateItem { get; set; }
}

internal class CreateSubItemResponse
{
    public CreateSubItemData? Data { get; set; }
}

internal class CreateSubItemData
{
    [JsonPropertyName("create_subitem")]
    public dynamic? CreateSubItem { get; set; }
}
