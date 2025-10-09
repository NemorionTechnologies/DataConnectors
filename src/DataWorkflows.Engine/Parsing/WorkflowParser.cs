using System.Text.Json;
using DataWorkflows.Engine.Models;

namespace DataWorkflows.Engine.Parsing;

public class WorkflowParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorkflowDefinition Parse(string json)
    {
        return JsonSerializer.Deserialize<WorkflowDefinition>(json, _options)
            ?? throw new ArgumentException("Invalid workflow JSON");
    }
}
