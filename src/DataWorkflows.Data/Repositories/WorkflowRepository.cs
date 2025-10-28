using Dapper;
using Npgsql;

namespace DataWorkflows.Data.Repositories;

public class WorkflowRepository
{
    private readonly string _connectionString;

    public WorkflowRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WorkflowRecord?> GetByIdAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = "SELECT * FROM Workflows WHERE Id = @Id";
        return await conn.QuerySingleOrDefaultAsync<WorkflowRecord>(sql, new { Id = workflowId });
    }

    public async Task<List<WorkflowRecord>> GetAllAsync(string? status = null, bool? isEnabled = null)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = "SELECT * FROM Workflows WHERE 1=1";

        if (status != null)
            sql += " AND Status = @Status";
        if (isEnabled.HasValue)
            sql += " AND IsEnabled = @IsEnabled";

        sql += " ORDER BY CreatedAt DESC";

        var results = await conn.QueryAsync<WorkflowRecord>(sql, new { Status = status, IsEnabled = isEnabled });
        return results.ToList();
    }

    public async Task<string> CreateDraftAsync(string workflowId, string displayName, string? description)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            INSERT INTO Workflows (Id, DisplayName, Description, Status, IsEnabled, CreatedAt, UpdatedAt)
            VALUES (@Id, @DisplayName, @Description, 'Draft', TRUE, NOW(), NOW())
            ON CONFLICT (Id) DO NOTHING
            RETURNING Id";

        var result = await conn.ExecuteScalarAsync<string>(sql, new
        {
            Id = workflowId,
            DisplayName = displayName,
            Description = description
        });

        return result ?? workflowId; // Return ID even if already exists
    }

    public async Task UpdateDraftAsync(string workflowId, string displayName, string? description)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            UPDATE Workflows
            SET DisplayName = @DisplayName, Description = @Description, UpdatedAt = NOW()
            WHERE Id = @Id AND Status = 'Draft'";

        var rowsAffected = await conn.ExecuteAsync(sql, new
        {
            Id = workflowId,
            DisplayName = displayName,
            Description = description
        });

        if (rowsAffected == 0)
        {
            var existing = await GetByIdAsync(workflowId);
            if (existing == null)
                throw new InvalidOperationException($"Workflow not found: {workflowId}");
            if (existing.Status != "Draft")
                throw new InvalidOperationException($"Cannot update workflow in {existing.Status} status. Only Draft workflows can be modified.");
        }
    }

    public async Task PublishAsync(string workflowId, int newVersion, bool autoActivate)
    {
        using var conn = new NpgsqlConnection(_connectionString);

        var status = autoActivate ? "Active" : "Draft";
        var isEnabled = autoActivate;

        var sql = @"
            UPDATE Workflows
            SET CurrentVersion = @Version, Status = @Status, IsEnabled = @IsEnabled, UpdatedAt = NOW()
            WHERE Id = @Id";

        await conn.ExecuteAsync(sql, new
        {
            Id = workflowId,
            Version = newVersion,
            Status = status,
            IsEnabled = isEnabled
        });
    }

    public async Task ArchiveAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            UPDATE Workflows
            SET Status = 'Archived', IsEnabled = FALSE, UpdatedAt = NOW()
            WHERE Id = @Id AND Status = 'Active'";

        var rowsAffected = await conn.ExecuteAsync(sql, new { Id = workflowId });

        if (rowsAffected == 0)
        {
            var existing = await GetByIdAsync(workflowId);
            if (existing == null)
                throw new InvalidOperationException($"Workflow not found: {workflowId}");
            if (existing.Status != "Active")
                throw new InvalidOperationException($"Cannot archive workflow in {existing.Status} status. Only Active workflows can be archived.");
        }
    }

    public async Task ReactivateAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            UPDATE Workflows
            SET Status = 'Active', IsEnabled = TRUE, UpdatedAt = NOW()
            WHERE Id = @Id AND Status = 'Archived'";

        var rowsAffected = await conn.ExecuteAsync(sql, new { Id = workflowId });

        if (rowsAffected == 0)
        {
            var existing = await GetByIdAsync(workflowId);
            if (existing == null)
                throw new InvalidOperationException($"Workflow not found: {workflowId}");
            if (existing.Status != "Archived")
                throw new InvalidOperationException($"Cannot reactivate workflow in {existing.Status} status. Only Archived workflows can be reactivated.");
        }
    }

    public async Task DeleteDraftAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = "DELETE FROM Workflows WHERE Id = @Id AND Status = 'Draft'";

        var rowsAffected = await conn.ExecuteAsync(sql, new { Id = workflowId });

        if (rowsAffected == 0)
        {
            var existing = await GetByIdAsync(workflowId);
            if (existing == null)
                throw new InvalidOperationException($"Workflow not found: {workflowId}");
            if (existing.Status != "Draft")
                throw new InvalidOperationException($"Cannot delete workflow in {existing.Status} status. Only Draft workflows can be deleted.");
        }
    }
}

public record WorkflowRecord(
    string Id,
    string DisplayName,
    string? Description,
    int? CurrentVersion,
    string Status,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
