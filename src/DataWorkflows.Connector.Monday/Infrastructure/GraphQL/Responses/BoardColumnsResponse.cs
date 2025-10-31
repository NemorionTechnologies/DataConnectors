using System.Collections.Generic;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL.Responses;

internal class BoardColumnsResponse
{
    public BoardColumnsData? Data { get; set; }
}

internal class BoardColumnsData
{
    public List<BoardWithColumns> Boards { get; set; } = new();
}

internal class BoardWithColumns
{
    public List<dynamic> Columns { get; set; } = new();
}
