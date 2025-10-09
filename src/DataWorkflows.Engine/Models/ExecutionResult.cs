namespace DataWorkflows.Engine.Models;

public record ExecutionResult(
    Guid ExecutionId,
    string Status,
    DateTime CompletedAt
);
