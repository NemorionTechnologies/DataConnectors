using Dapper;
using Npgsql;

namespace DataWorkflows.Data.Repositories;

public class ActionExecutionRepository
{
    private readonly string _connectionString;

    public ActionExecutionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task RecordExecution(
        Guid executionId,
        string nodeId,
        string actionType,
        string status,
        int attempt,
        int retryCount,
        string? outputs,
        string? error,
        DateTime startTime,
        DateTime endTime
    )
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            INSERT INTO ActionExecutions (
                WorkflowExecutionId,
                NodeId,
                ActionType,
                Status,
                Attempt,
                RetryCount,
                OutputsJson,
                ErrorJson,
                StartTime,
                EndTime)
            VALUES (
                @ExecutionId,
                @NodeId,
                @ActionType,
                @Status,
                @Attempt,
                @RetryCount,
                @Outputs::jsonb,
                @Error::jsonb,
                @StartTime,
                @EndTime)";

        await conn.ExecuteAsync(sql, new
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            ActionType = actionType,
            Status = status,
            Attempt = attempt,
            RetryCount = retryCount,
            Outputs = outputs,
            Error = error,
            StartTime = startTime,
            EndTime = endTime
        });
    }
}
