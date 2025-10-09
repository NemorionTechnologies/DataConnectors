namespace DataWorkflows.Contracts.Actions;

public sealed record ActionExecutionContext(
    Guid WorkflowExecutionId,
    string NodeId,
    Dictionary<string, object?> Parameters,
    IServiceProvider Services
);
