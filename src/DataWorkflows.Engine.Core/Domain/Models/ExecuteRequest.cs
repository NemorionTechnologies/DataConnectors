namespace DataWorkflows.Engine.Core.Domain.Models;

public record ExecuteRequest(
    Dictionary<string, object>? Trigger,
    Dictionary<string, object>? Vars
);
