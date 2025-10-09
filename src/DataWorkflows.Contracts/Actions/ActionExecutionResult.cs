namespace DataWorkflows.Contracts.Actions;

public sealed record ActionExecutionResult(
    ActionExecutionStatus Status,
    Dictionary<string, object?> Outputs,
    string? ErrorMessage = null
);
