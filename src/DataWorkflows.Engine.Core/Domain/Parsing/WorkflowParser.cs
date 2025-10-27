using System.Text.Json;
using DataWorkflows.Engine.Core.Domain.Models;

namespace DataWorkflows.Engine.Core.Domain.Parsing;

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
