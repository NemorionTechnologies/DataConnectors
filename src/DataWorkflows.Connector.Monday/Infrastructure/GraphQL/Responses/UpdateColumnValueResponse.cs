using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class UpdateColumnValueResponse
{
    public UpdateColumnValueData? Data { get; set; }
}

internal class UpdateColumnValueData
{
    [JsonPropertyName("change_column_value")]
    public dynamic? ChangeColumnValue { get; set; }
}
