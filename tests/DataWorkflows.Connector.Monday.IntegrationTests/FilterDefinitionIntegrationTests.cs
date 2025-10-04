using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.DTOs;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

public class FilterDefinitionIntegrationTests : MondayIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public FilterDefinitionIntegrationTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task TypedFilters_ShouldExecuteAgainstMonday()
    {
        Config.Validate();

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

        await using var scope = configuredFactory.Services.CreateAsyncScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();

        var cases = await MondayFilterDefinitionFixtures.BuildCasesAsync(Config, columnResolver);

        foreach (var filterCase in cases)
        {
            await ExecuteCaseAsync(apiClient, filterCase);
        }
    }

    private async Task ExecuteCaseAsync(IMondayApiClient apiClient, MondayFilterDefinitionFixtures.FilterDefinitionCase filterCase)
    {
        _output.WriteLine($"\n===== Running filter case: {filterCase.Name} =====");

        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            filterCase.Definition,
            CancellationToken.None)).ToList();

        _output.WriteLine($"Returned {items.Count} items");
        foreach (var item in items.Take(5))
        {
            _output.WriteLine($"  - {item.Title} ({item.Id})");
        }

        items.Should().NotBeNull();
    }
}
