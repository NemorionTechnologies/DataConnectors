using DataWorkflows.Contracts.Actions;

namespace DataWorkflows.Engine.Actions;

public class CoreEchoAction : IWorkflowAction
{
    public string Type => "core.echo";

    public Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var message = context.Parameters.TryGetValue("message", out var msg) ? msg?.ToString() : "echo";

        return Task.FromResult(new ActionExecutionResult(
            Status: ActionExecutionStatus.Succeeded,
            Outputs: new Dictionary<string, object?> { ["echo"] = message }
        ));
    }
}
