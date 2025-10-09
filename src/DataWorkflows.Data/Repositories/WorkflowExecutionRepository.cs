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
    string? CorrelationId
);
