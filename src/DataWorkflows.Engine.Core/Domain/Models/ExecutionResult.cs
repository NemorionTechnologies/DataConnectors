namespace DataWorkflows.Engine.Core.Domain.Models;

public record ExecutionResult(
    Guid ExecutionId,
    string Status,
    DateTime CompletedAt
);
