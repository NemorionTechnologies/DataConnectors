using System.Reflection;
using Dapper;
using Npgsql;

namespace DataWorkflows.Data.Migrations;

public static class MigrationRunner
{
    private const string AppliedTableSql = @"
        CREATE TABLE IF NOT EXISTS schema_migrations (
            name TEXT PRIMARY KEY,
            applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );";

    public static async Task ApplyAll(string connectionString)
    {
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(AppliedTableSql);

        var asm = Assembly.GetExecutingAssembly();
        var resources = asm.GetManifestResourceNames()
            .Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .ToList();

        foreach (var res in resources)
        {
            var applied = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM schema_migrations WHERE name = @name", new { name = res });
            if (applied > 0)
            {
                continue;
            }

            using var stream = asm.GetManifestResourceStream(res) ?? throw new InvalidOperationException($"Missing resource: {res}");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync();
            await conn.ExecuteAsync(sql);
            await conn.ExecuteAsync("INSERT INTO schema_migrations(name) VALUES(@name)", new { name = res });
        }
    }
}

