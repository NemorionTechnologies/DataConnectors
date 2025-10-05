using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

/// <summary>
/// Integration tests for filter complexity guardrails against real Monday API.
/// Tests both validation enforcement and behavior at complexity boundaries.
/// </summary>
public class GuardrailIntegrationTests : MondayIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public GuardrailIntegrationTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task ServerSideTranslation_ShouldExecuteSimpleAndChains()
    {
        Config.Validate();

        var configuredFactory = CreateConfiguredFactory();
        await using var scope = configuredFactory.Services.CreateAsyncScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();
        var translator = scope.ServiceProvider.GetRequiredService<IMondayFilterTranslator>();

        var statusColumnId = await columnResolver.ResolveColumnIdAsync(
            Config.BoardId,
            columnId: null,
            Config.StatusColumnTitle,
            CancellationToken.None);

        _output.WriteLine($"Testing server-side translation with simple AND chain (Status = '{Config.StatusLabel}')...");

        // Simple single-rule filter that should translate server-side
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, Config.StatusLabel)
            },
            CreatedAt: null,
            Condition: null);

        var translationResult = translator.Translate(filter);
        translationResult.QueryParams.Should().NotBeNull("simple AND chains should translate server-side");
        translationResult.QueryParams!.Rules.Should().HaveCount(1);

        _output.WriteLine($"Server-side rules: {translationResult.QueryParams.Rules.Count}");
        foreach (var rule in translationResult.QueryParams.Rules)
        {
            _output.WriteLine($"  - Column: {rule.ColumnId}, Operator: {rule.Operator}, Value: {rule.CompareValue}");
        }

        // Execute against Monday API
        var items = await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            filter,
            CancellationToken.None);

        var itemList = items.ToList();
        _output.WriteLine($"Retrieved {itemList.Count} items via server-side filtering");

        itemList.Should().NotBeNull();
        itemList.Should().AllSatisfy(item => item.Id.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task MaxComplexityFilter_ShouldExecuteSuccessfully()
    {
        Config.Validate();

        var configuredFactory = CreateConfiguredFactory();
        await using var scope = configuredFactory.Services.CreateAsyncScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<GuardrailOptions>>();

        _output.WriteLine($"Testing filter at maximum complexity...");
        _output.WriteLine($"Guardrail limits: MaxDepth={options.Value.MaxDepth}, MaxTotalRuleCount={options.Value.MaxTotalRuleCount}");

        var statusColumnId = await columnResolver.ResolveColumnIdAsync(
            Config.BoardId,
            columnId: null,
            Config.StatusColumnTitle,
            CancellationToken.None);

        // Build filter at exactly the maximum allowed complexity
        var maxDepth = options.Value.MaxDepth;
        var maxRules = options.Value.MaxTotalRuleCount;

        // Create nested groups up to max depth with rules distributed evenly
        var filter = BuildMaxComplexityFilter(statusColumnId, maxDepth, maxRules);

        _output.WriteLine($"Filter complexity: Depth={maxDepth}, Rules={maxRules}");

        // Should NOT throw exception - at the limit is allowed
        var items = await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            filter,
            CancellationToken.None);

        var itemList = items.ToList();
        _output.WriteLine($"Retrieved {itemList.Count} items with max complexity filter");

        itemList.Should().NotBeNull();
    }

    [Fact]
    public async Task ExceedMaxDepth_ShouldThrowGuardrailException()
    {
        Config.Validate();

        var configuredFactory = CreateConfiguredFactory();
        await using var scope = configuredFactory.Services.CreateAsyncScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<GuardrailOptions>>();

        _output.WriteLine($"Testing filter EXCEEDING maximum depth...");

        var statusColumnId = await columnResolver.ResolveColumnIdAsync(
            Config.BoardId,
            columnId: null,
            Config.StatusColumnTitle,
            CancellationToken.None);

        var maxDepth = options.Value.MaxDepth;
        var exceedingDepth = maxDepth + 1;

        _output.WriteLine($"Max allowed depth: {maxDepth}, Filter depth: {exceedingDepth}");

        // Build deeply nested filter that exceeds max depth
        var filter = BuildDeeplyNestedFilter(statusColumnId, exceedingDepth);

        var act = async () => await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            filter,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GuardrailViolationException>();
        exception.Which.Message.Should().Contain("depth");
        exception.Which.Message.Should().Contain(exceedingDepth.ToString());
        exception.Which.Message.Should().Contain(maxDepth.ToString());

        _output.WriteLine($"Correctly threw GuardrailViolationException: {exception.Which.Message}");
    }

    [Fact]
    public async Task ExceedMaxRuleCount_ShouldThrowGuardrailException()
    {
        Config.Validate();

        var configuredFactory = CreateConfiguredFactory();
        await using var scope = configuredFactory.Services.CreateAsyncScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<GuardrailOptions>>();

        _output.WriteLine($"Testing filter EXCEEDING maximum rule count...");

        var statusColumnId = await columnResolver.ResolveColumnIdAsync(
            Config.BoardId,
            columnId: null,
            Config.StatusColumnTitle,
            CancellationToken.None);

        var maxRules = options.Value.MaxTotalRuleCount;
        var exceedingRuleCount = maxRules + 1;

        _output.WriteLine($"Max allowed rules: {maxRules}, Filter rule count: {exceedingRuleCount}");

        // Build filter with too many rules
        var rules = new List<MondayFilterRule>();
        for (var i = 0; i < exceedingRuleCount; i++)
        {
            rules.Add(new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, $"Value{i}"));
        }

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: rules,
            CreatedAt: null,
            Condition: null);

        var act = async () => await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            filter,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GuardrailViolationException>();
        exception.Which.Message.Should().Contain(exceedingRuleCount.ToString());
        exception.Which.Message.Should().Contain(maxRules.ToString());

        _output.WriteLine($"Correctly threw GuardrailViolationException: {exception.Which.Message}");
    }

    [Fact]
    public async Task DisabledGuardrails_ShouldAllowComplexFilters()
    {
        Config.Validate();

        // Configure factory with guardrails disabled
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId,
                    ["Guardrails:IsEnabled"] = "false" // Disable guardrails
                }!);
            });
        });

        await using var scope = configuredFactory.Services.CreateAsyncScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnResolver = scope.ServiceProvider.GetRequiredService<IColumnResolverService>();

        _output.WriteLine($"Testing with guardrails DISABLED...");

        var statusColumnId = await columnResolver.ResolveColumnIdAsync(
            Config.BoardId,
            columnId: null,
            Config.StatusColumnTitle,
            CancellationToken.None);

        // Create a filter that would normally exceed limits
        var filter = BuildDeeplyNestedFilter(statusColumnId, depth: 10);

        _output.WriteLine($"Filter depth: 10 (would normally exceed limits)");

        // Should NOT throw - guardrails disabled
        var items = await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            filter,
            CancellationToken.None);

        var itemList = items.ToList();
        _output.WriteLine($"Retrieved {itemList.Count} items with guardrails disabled");

        itemList.Should().NotBeNull();
    }

    private WebApplicationFactory<Program> CreateConfiguredFactory()
    {
        return Factory.WithWebHostBuilder(builder =>
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
    }

    private MondayFilterDefinition BuildMaxComplexityFilter(string columnId, int maxDepth, int maxRules)
    {
        // Distribute rules across depth to hit exactly maxRules
        var rulesPerLevel = Math.Max(1, maxRules / maxDepth);
        var remainingRules = maxRules - (rulesPerLevel * maxDepth);

        MondayFilterConditionGroup BuildNestedGroup(int depth, int rulesAtThisLevel)
        {
            var rules = new List<MondayFilterRule>();
            for (var i = 0; i < rulesAtThisLevel; i++)
            {
                rules.Add(new MondayFilterRule(columnId, MondayFilterOperators.EqualsOperator, $"Value{depth}_{i}"));
            }

            if (depth == 1)
            {
                return new MondayFilterConditionGroup(
                    Rules: rules,
                    All: null,
                    Any: null,
                    Not: null);
            }

            return new MondayFilterConditionGroup(
                Rules: rules,
                All: new[] { BuildNestedGroup(depth - 1, rulesPerLevel) },
                Any: null,
                Not: null);
        }

        return new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: BuildNestedGroup(maxDepth, rulesPerLevel + remainingRules));
    }

    private MondayFilterDefinition BuildDeeplyNestedFilter(string columnId, int depth)
    {
        MondayFilterConditionGroup BuildNestedGroup(int currentDepth)
        {
            if (currentDepth == 1)
            {
                return new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule(columnId, MondayFilterOperators.EqualsOperator, "TestValue") },
                    All: null,
                    Any: null,
                    Not: null);
            }

            return new MondayFilterConditionGroup(
                Rules: null,
                All: new[] { BuildNestedGroup(currentDepth - 1) },
                Any: null,
                Not: null);
        }

        return new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: BuildNestedGroup(depth));
    }
}
