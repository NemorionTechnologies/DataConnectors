using System;
using Dapper;
using Npgsql;
using DataWorkflows.Data.Migrations;

namespace DataWorkflows.Engine.Tests.Data;

public static class TestDatabase
{
    private static readonly string MasterConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    public static string GetConnectionString()
    {
        var dbName = "test_dataworkflows_" + Guid.NewGuid().ToString("N");
        CreateDatabase(dbName);

        var connectionString = BuildConnectionString(dbName);

        // Apply migrations
        MigrationRunner.ApplyAll(connectionString).Wait();

        return connectionString;
    }

    public static void Cleanup(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var dbName = builder.Database;

        if (dbName != null && dbName.StartsWith("test_dataworkflows_"))
        {
            DropDatabase(dbName);
        }
    }

    private static void CreateDatabase(string dbName)
    {
        using var conn = new NpgsqlConnection(MasterConnectionString);
        conn.Open();

        // Close any existing connections to this database
        conn.Execute($@"
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{dbName}' AND pid <> pg_backend_pid();
        ");

        conn.Execute($"CREATE DATABASE {dbName}");
    }

    private static void DropDatabase(string dbName)
    {
        try
        {
            using var conn = new NpgsqlConnection(MasterConnectionString);
            conn.Open();

            // Force close connections
            conn.Execute($@"
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{dbName}' AND pid <> pg_backend_pid();
            ");

            conn.Execute($"DROP DATABASE IF EXISTS {dbName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to drop test database {dbName}: {ex.Message}");
        }
    }

    private static string BuildConnectionString(string dbName)
    {
        var builder = new NpgsqlConnectionStringBuilder(MasterConnectionString)
        {
            Database = dbName
        };
        return builder.ToString();
    }
}
