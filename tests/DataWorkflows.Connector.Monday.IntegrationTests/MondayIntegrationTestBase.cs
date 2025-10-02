using DataWorkflows.Connector.Monday.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

/// <summary>
/// Base class for Monday.com integration tests that properly configures the test environment.
/// Handles loading test configuration and injecting it into the application's DI container.
/// </summary>
public abstract class MondayIntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly TestConfiguration Config;

    protected MondayIntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Config = TestConfiguration.Load();

        // Configure the factory with test configuration once
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        Client = configuredFactory.CreateClient();
    }

    /// <summary>
    /// Resolves a column title to its column ID using the column resolver service.
    /// Uses the caching layer automatically by accessing the properly configured DI container.
    /// </summary>
    protected async Task<string> ResolveColumnIdAsync(string columnTitle, CancellationToken cancellationToken = default)
    {
        // Need to use the configured factory that has the API key set
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();

        return await columnResolver.ResolveColumnIdAsync(
            Config.BoardId,
            columnId: null,
            columnTitle: columnTitle,
            cancellationToken);
    }
}
