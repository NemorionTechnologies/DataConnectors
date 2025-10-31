using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class BoardActivityResponse
{
    public BoardActivityData? Data { get; set; }
}

internal class BoardActivityData
{
    public List<BoardWithActivityLogs> Boards { get; set; } = new();
}

internal class BoardWithActivityLogs
{
    [JsonPropertyName("activity_logs")]
    public List<dynamic> ActivityLogs { get; set; } = new();
}
