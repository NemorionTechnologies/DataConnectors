namespace DataWorkflows.Engine.Models;

public sealed record WorkflowDefinition(
    string Id,
    string DisplayName,
    string StartNode,
    List<Node> Nodes
);
