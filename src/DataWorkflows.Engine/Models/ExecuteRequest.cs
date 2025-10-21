namespace DataWorkflows.Engine.Models;

public record ExecuteRequest(
    Dictionary<string, object>? Trigger,
    Dictionary<string, object>? Vars
);
