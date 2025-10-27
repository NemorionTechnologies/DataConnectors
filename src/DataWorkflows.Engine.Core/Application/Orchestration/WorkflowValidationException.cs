using System;

namespace DataWorkflows.Engine.Core.Application.Orchestration;

public sealed class WorkflowValidationException : Exception
{
    public WorkflowValidationException(string workflowId, Exception inner)
        : base($"Workflow '{workflowId}' failed validation.", inner)
    {
        WorkflowId = workflowId;
    }

    public string WorkflowId { get; }
}
