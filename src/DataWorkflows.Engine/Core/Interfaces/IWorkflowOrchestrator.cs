namespace DataWorkflows.Engine.Core.Interfaces;

/// <summary>
/// Defines the contract for workflow orchestration.
/// </summary>
public interface IWorkflowOrchestrator
{
    Task<string> ExecuteWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
}
