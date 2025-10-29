using Dapper;
using DataWorkflows.Data.Models;
using Npgsql;

namespace DataWorkflows.Data.Repositories;

/// <summary>
/// PostgreSQL implementation of IActionCatalogRepository.
/// </summary>
public sealed class ActionCatalogRepository : IActionCatalogRepository
{
    private readonly string _connectionString;

    public ActionCatalogRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<int> UpsertActionsAsync(
        string connectorId,
        IEnumerable<ActionCatalogEntry> actions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
            throw new ArgumentException("ConnectorId cannot be null or whitespace.", nameof(connectorId));

        if (actions == null)
            throw new ArgumentNullException(nameof(actions));

        var actionsList = actions.ToList();
        if (actionsList.Count == 0)
            return 0;

        const string sql = @"
            INSERT INTO ActionCatalog (
                ActionType, ConnectorId, DisplayName, Description,
                ParameterSchema, OutputSchema, IsEnabled, RequiresAuth,
                CreatedAt, UpdatedAt
            )
            VALUES (
                @ActionType, @ConnectorId, @DisplayName, @Description,
                @ParameterSchemaJson::jsonb, @OutputSchemaJson::jsonb, @IsEnabled, @RequiresAuth,
                NOW(), NOW()
            )
            ON CONFLICT (ConnectorId, ActionType)
            DO UPDATE SET
                DisplayName = EXCLUDED.DisplayName,
                Description = EXCLUDED.Description,
                ParameterSchema = EXCLUDED.ParameterSchema,
                OutputSchema = EXCLUDED.OutputSchema,
                IsEnabled = EXCLUDED.IsEnabled,
                RequiresAuth = EXCLUDED.RequiresAuth,
                UpdatedAt = NOW()";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteAsync(sql, actionsList);
        return result;
    }

    public async Task<ActionCatalogEntry?> GetByActionTypeAsync(
        string actionType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("ActionType cannot be null or whitespace.", nameof(actionType));

        const string sql = @"
            SELECT
                Id, ActionType, ConnectorId, DisplayName, Description,
                ParameterSchema::text AS ParameterSchemaJson,
                OutputSchema::text AS OutputSchemaJson,
                IsEnabled, RequiresAuth, CreatedAt, UpdatedAt
            FROM ActionCatalog
            WHERE ActionType = @ActionType";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ActionCatalogEntry>(
            sql,
            new { ActionType = actionType });
    }

    public async Task<IReadOnlyList<ActionCatalogEntry>> GetByConnectorIdAsync(
        string connectorId,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
            throw new ArgumentException("ConnectorId cannot be null or whitespace.", nameof(connectorId));

        var sql = @"
            SELECT
                Id, ActionType, ConnectorId, DisplayName, Description,
                ParameterSchema::text AS ParameterSchemaJson,
                OutputSchema::text AS OutputSchemaJson,
                IsEnabled, RequiresAuth, CreatedAt, UpdatedAt
            FROM ActionCatalog
            WHERE ConnectorId = @ConnectorId";

        if (!includeDisabled)
        {
            sql += " AND IsEnabled = TRUE";
        }

        sql += " ORDER BY ActionType";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<ActionCatalogEntry>(
            sql,
            new { ConnectorId = connectorId });

        return results.ToList();
    }

    public async Task<IReadOnlyList<ActionCatalogEntry>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                Id, ActionType, ConnectorId, DisplayName, Description,
                ParameterSchema::text AS ParameterSchemaJson,
                OutputSchema::text AS OutputSchemaJson,
                IsEnabled, RequiresAuth, CreatedAt, UpdatedAt
            FROM ActionCatalog
            WHERE IsEnabled = TRUE
            ORDER BY ConnectorId, ActionType";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<ActionCatalogEntry>(sql);
        return results.ToList();
    }

    public async Task<IReadOnlyList<ActionCatalogEntry>> GetAllAsync(
        bool? isEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                Id, ActionType, ConnectorId, DisplayName, Description,
                ParameterSchema::text AS ParameterSchemaJson,
                OutputSchema::text AS OutputSchemaJson,
                IsEnabled, RequiresAuth, CreatedAt, UpdatedAt
            FROM ActionCatalog";

        if (isEnabled.HasValue)
        {
            sql += " WHERE IsEnabled = @IsEnabled";
        }

        sql += " ORDER BY ConnectorId, ActionType";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<ActionCatalogEntry>(
            sql,
            new { IsEnabled = isEnabled });

        return results.ToList();
    }
}
