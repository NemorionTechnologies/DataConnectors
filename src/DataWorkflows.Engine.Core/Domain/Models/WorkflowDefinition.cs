namespace DataWorkflows.Engine.Core.Domain.Models;

public sealed record WorkflowDefinition(
    string Id,
    string DisplayName,
    string StartNode,
    List<Node> Nodes
);
