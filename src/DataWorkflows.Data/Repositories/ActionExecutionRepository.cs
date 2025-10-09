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
        string? outputs,
        DateTime startTime,
        DateTime endTime
    )
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            INSERT INTO ActionExecutions (WorkflowExecutionId, NodeId, ActionType, Status, OutputsJson, StartTime, EndTime)
            VALUES (@ExecutionId, @NodeId, @ActionType, @Status, @Outputs::jsonb, @StartTime, @EndTime)";

        await conn.ExecuteAsync(sql, new
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            ActionType = actionType,
            Status = status,
            Outputs = outputs,
            StartTime = startTime,
            EndTime = endTime
        });
    }
}
