namespace DataWorkflows.Contracts.Actions;

public enum ActionExecutionStatus
{
    Succeeded,
    Failed,
    RetriableFailure,
    Skipped
}
