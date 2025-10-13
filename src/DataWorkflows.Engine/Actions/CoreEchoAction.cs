using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataWorkflows.Contracts.Actions;

namespace DataWorkflows.Engine.Actions;

public class CoreEchoAction : IWorkflowAction
{
    public string Type => "core.echo";
    private readonly ConcurrentDictionary<(Guid ExecutionId, string NodeId), int> _attempts = new();

    public Task<ActionExecutionResult> ExecuteAsync(ActionExecutionContext context, CancellationToken ct)
    {
        var message = context.Parameters.TryGetValue("message", out var msg) ? msg?.ToString() : "echo";

        if (context.Parameters.TryGetValue("simulateFailure", out var simulate))
        {
            var failType = ExtractFailureType(simulate);
            if (!string.IsNullOrEmpty(failType))
            {
                var key = (context.WorkflowExecutionId, context.NodeId);
                var attempt = _attempts.AddOrUpdate(key, 1, static (_, current) => current + 1);
                failType = failType.ToLower(CultureInfo.InvariantCulture);

                if (failType == "transient" && attempt < 3)
                {
                    return Task.FromResult(new ActionExecutionResult(
                        ActionExecutionStatus.RetriableFailure,
                        new Dictionary<string, object?>(),
                        $"Simulated transient failure (attempt {attempt})"
                    ));
                }

                if (failType == "permanent")
                {
                    return Task.FromResult(new ActionExecutionResult(
                        ActionExecutionStatus.Failed,
                        new Dictionary<string, object?>(),
                        "Simulated permanent failure"
                    ));
                }

                _ = _attempts.TryRemove(key, out _);
            }
        }
        else
        {
            _ = _attempts.TryRemove((context.WorkflowExecutionId, context.NodeId), out _);
        }

        return Task.FromResult(new ActionExecutionResult(
            ActionExecutionStatus.Succeeded,
            new Dictionary<string, object?> { ["echo"] = message }
        ));
    }

    private static string? ExtractFailureType(object? simulate)
    {
        return simulate switch
        {
            string s => s,
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            _ => null
        };
    }
}
