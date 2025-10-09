namespace DataWorkflows.Engine.Models;

public sealed record Edge(
    string TargetNode,
    string When = "success",
    string? Condition = null
);
