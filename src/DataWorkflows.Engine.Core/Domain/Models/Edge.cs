namespace DataWorkflows.Engine.Core.Domain.Models;

public sealed record Edge(
    string TargetNode,
    string When = "success",
    string? Condition = null
);
