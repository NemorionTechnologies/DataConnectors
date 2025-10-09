using DataWorkflows.Contracts.Actions;
using DataWorkflows.Data.Repositories;
using DataWorkflows.Engine.Execution;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Registry;

namespace DataWorkflows.Engine.Orchestration;

public class WorkflowConductor
{
    private readonly ActionRegistry _registry;

    public WorkflowConductor(ActionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object> trigger,
        string requestId,
        string connectionString
    )
    {
        // Create WorkflowExecution record (Single Responsibility: own the full execution lifecycle)
        var executionRepo = new WorkflowExecutionRepository(connectionString);
        var executionId = await executionRepo.CreateExecution(
            workflowId: workflow.Id,
            version: 1,
            requestId: requestId,
            triggerJson: System.Text.Json.JsonSerializer.Serialize(trigger)
        );

        var context = new WorkflowContext();
        var actionRepo = new ActionExecutionRepository(connectionString);

        // Simple linear execution (ignore edges for now - Bundle 3 will add branching)
        foreach (var node in workflow.Nodes)
        {
            var startTime = DateTime.UtcNow;
            var action = _registry.GetAction(node.ActionType);

            var actionContext = new ActionExecutionContext(
                WorkflowExecutionId: executionId,
                NodeId: node.Id,
                Parameters: node.Parameters ?? new(),
                Services: null!
            );

            var result = await action.ExecuteAsync(actionContext, CancellationToken.None);

            await actionRepo.RecordExecution(
                executionId: executionId,
                nodeId: node.Id,
                actionType: node.ActionType,
                status: result.Status.ToString(),
                outputs: System.Text.Json.JsonSerializer.Serialize(result.Outputs),
                startTime: startTime,
                endTime: DateTime.UtcNow
            );

            if (result.Status == ActionExecutionStatus.Succeeded)
            {
                context.SetActionOutput(node.Id, result.Outputs);
            }
            else
            {
                throw new Exception($"Action failed: {result.ErrorMessage}");
            }
        }

        return new ExecutionResult(
            ExecutionId: executionId,
            Status: "Succeeded",
            CompletedAt: DateTime.UtcNow
        );
    }
}
