using Dapper;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace DataWorkflows.Data.Repositories;

public class WorkflowDefinitionRepository
{
    private readonly string _connectionString;

    public WorkflowDefinitionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WorkflowDefinitionRecord?> GetByIdAndVersionAsync(string workflowId, int version)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT * FROM WorkflowDefinitions
            WHERE WorkflowId = @WorkflowId AND Version = @Version";

        return await conn.QuerySingleOrDefaultAsync<WorkflowDefinitionRecord>(sql, new
        {
            WorkflowId = workflowId,
            Version = version
        });
    }

    public async Task<WorkflowDefinitionRecord?> GetLatestVersionAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT * FROM WorkflowDefinitions
            WHERE WorkflowId = @WorkflowId
            ORDER BY Version DESC
            LIMIT 1";

        return await conn.QuerySingleOrDefaultAsync<WorkflowDefinitionRecord>(sql, new { WorkflowId = workflowId });
    }

    public async Task<WorkflowDefinitionRecord?> GetDraftVersionAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT * FROM WorkflowDefinitions
            WHERE WorkflowId = @WorkflowId AND Version = 0";

        return await conn.QuerySingleOrDefaultAsync<WorkflowDefinitionRecord>(sql, new { WorkflowId = workflowId });
    }

    public async Task<List<WorkflowDefinitionRecord>> GetAllVersionsAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT * FROM WorkflowDefinitions
            WHERE WorkflowId = @WorkflowId
            ORDER BY Version DESC";

        var results = await conn.QueryAsync<WorkflowDefinitionRecord>(sql, new { WorkflowId = workflowId });
        return results.ToList();
    }

    public async Task<(int newVersion, bool created)> CreateOrUpdateDraftAsync(string workflowId, string definitionJson)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var checksum = ComputeChecksum(definitionJson);

        // Check if draft (version 0) already exists
        var existing = await GetDraftVersionAsync(workflowId);

        if (existing != null)
        {
            // Update existing draft
            var updateSql = @"
                UPDATE WorkflowDefinitions
                SET DefinitionJson = @DefinitionJson::jsonb, Checksum = @Checksum
                WHERE WorkflowId = @WorkflowId AND Version = 0";

            await conn.ExecuteAsync(updateSql, new
            {
                WorkflowId = workflowId,
                DefinitionJson = definitionJson,
                Checksum = checksum
            });

            return (0, false);
        }
        else
        {
            // Create new draft
            var insertSql = @"
                INSERT INTO WorkflowDefinitions (WorkflowId, Version, DefinitionJson, Checksum, CreatedAt)
                VALUES (@WorkflowId, 0, @DefinitionJson::jsonb, @Checksum, NOW())";

            await conn.ExecuteAsync(insertSql, new
            {
                WorkflowId = workflowId,
                DefinitionJson = definitionJson,
                Checksum = checksum
            });

            return (0, true);
        }
    }

    public async Task<(int version, bool created)> PublishVersionAsync(string workflowId, string definitionJson)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var checksum = ComputeChecksum(definitionJson);

        // Check if this exact definition already published
        var existingSql = @"
            SELECT Version FROM WorkflowDefinitions
            WHERE WorkflowId = @WorkflowId AND Checksum = @Checksum AND Version > 0
            ORDER BY Version DESC
            LIMIT 1";

        var existingVersion = await conn.QuerySingleOrDefaultAsync<int?>(existingSql, new
        {
            WorkflowId = workflowId,
            Checksum = checksum
        });

        if (existingVersion.HasValue)
        {
            // Already published with this checksum, return existing version (idempotent)
            return (existingVersion.Value, false);
        }

        // Get next version number
        var latestVersion = await GetLatestVersionAsync(workflowId);
        var nextVersion = (latestVersion?.Version ?? 0) + 1;

        // Create new version
        var insertSql = @"
            INSERT INTO WorkflowDefinitions (WorkflowId, Version, DefinitionJson, Checksum, CreatedAt)
            VALUES (@WorkflowId, @Version, @DefinitionJson::jsonb, @Checksum, NOW())";

        await conn.ExecuteAsync(insertSql, new
        {
            WorkflowId = workflowId,
            Version = nextVersion,
            DefinitionJson = definitionJson,
            Checksum = checksum
        });

        return (nextVersion, true);
    }

    public async Task DeleteDraftAsync(string workflowId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = "DELETE FROM WorkflowDefinitions WHERE WorkflowId = @WorkflowId AND Version = 0";
        await conn.ExecuteAsync(sql, new { WorkflowId = workflowId });
    }

    private static string ComputeChecksum(string json)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public record WorkflowDefinitionRecord(
    string WorkflowId,
    int Version,
    string DefinitionJson,
    string Checksum,
    DateTime CreatedAt
);
