namespace DataWorkflows.Contracts.Actions;

public interface IWorkflowAction
{
    string Type { get; }
    Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct);
}
