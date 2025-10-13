using Dapper;
using Npgsql;

namespace DataWorkflows.Data.Repositories;

public class WorkflowExecutionRepository
{
    private readonly string _connectionString;

    public WorkflowExecutionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> CreateExecution(string workflowId, int version, string requestId, string triggerJson)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            INSERT INTO WorkflowExecutions (WorkflowId, WorkflowVersion, WorkflowRequestId, Status, TriggerPayloadJson, CorrelationId)
            VALUES (@WorkflowId, @Version, @RequestId, 'Pending', @TriggerJson::jsonb, @CorrelationId)
            RETURNING Id";

        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            WorkflowId = workflowId,
            Version = version,
            RequestId = requestId,
            TriggerJson = triggerJson,
            CorrelationId = Guid.NewGuid().ToString()
        });
    }

    public async Task MarkExecutionRunning(Guid executionId, DateTime startTime)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            UPDATE WorkflowExecutions
               SET Status = 'Running',
                   StartTime = COALESCE(StartTime, @StartTime)
             WHERE Id = @ExecutionId";

        await conn.ExecuteAsync(sql, new
        {
            ExecutionId = executionId,
            StartTime = startTime
        });
    }

    public async Task CompleteExecution(Guid executionId, string status, DateTime endTime, string contextSnapshotJson)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            UPDATE WorkflowExecutions
               SET Status = @Status,
                   EndTime = @EndTime,
                   ContextSnapshotJson = @ContextSnapshot::jsonb
             WHERE Id = @ExecutionId";

        await conn.ExecuteAsync(sql, new
        {
            ExecutionId = executionId,
            Status = status,
            EndTime = endTime,
            ContextSnapshot = contextSnapshotJson
        });
    }

    public async Task<WorkflowExecution?> GetById(Guid id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = "SELECT * FROM WorkflowExecutions WHERE Id = @Id";
        return await conn.QuerySingleOrDefaultAsync<WorkflowExecution>(sql, new { Id = id });
    }
}

public record WorkflowExecution(
    Guid Id,
    string WorkflowId,
    int WorkflowVersion,
    string WorkflowRequestId,
    string Status,
    string TriggerPayloadJson,
    DateTime? StartTime,
    DateTime? EndTime,
    string? CorrelationId,
    string? ContextSnapshotJson
);
