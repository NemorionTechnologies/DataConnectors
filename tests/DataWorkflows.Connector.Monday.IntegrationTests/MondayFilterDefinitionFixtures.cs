using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

public static class MondayFilterDefinitionFixtures
{
    public static async Task<IReadOnlyList<FilterDefinitionCase>> BuildCasesAsync(
        TestConfiguration config,
        IColumnResolverService columnResolver,
        CancellationToken cancellationToken = default)
    {
        var statusColumnId = await ResolveColumnIdAsync(config.BoardId, config.StatusColumnTitle, columnResolver, cancellationToken);
        var linkColumnId = await ResolveColumnIdAsync(config.BoardId, config.LinkColumnTitle, columnResolver, cancellationToken);
        var statusLabel = string.IsNullOrWhiteSpace(config.StatusLabel) ? "Working on it" : config.StatusLabel;

        var cases = new List<FilterDefinitionCase>
        {
            new(
                "Status Equals Label",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: new[]
                    {
                        new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel)
                    },
                    CreatedAt: null,
                    Condition: null)),

            new(
                "Status Equals Label And Link Empty",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: Array.Empty<MondayFilterRule>(),
                    CreatedAt: null,
                    Condition: new MondayFilterConditionGroup(
                        Rules: null,
                        All: new[]
                        {
                            new MondayFilterConditionGroup(
                                Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel) },
                                All: null,
                                Any: null,
                                Not: null),
                            new MondayFilterConditionGroup(
                                Rules: new[] { new MondayFilterRule(linkColumnId, MondayFilterOperators.IsEmptyOperator, null) },
                                All: null,
                                Any: null,
                                Not: null)
                        },
                        Any: null,
                        Not: null))),

            new(
                "Status Equals Label Or Link Empty",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: Array.Empty<MondayFilterRule>(),
                    CreatedAt: null,
                    Condition: new MondayFilterConditionGroup(
                        Rules: null,
                        All: null,
                        Any: new[]
                        {
                            new MondayFilterConditionGroup(
                                Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel) },
                                All: null,
                                Any: null,
                                Not: null),
                            new MondayFilterConditionGroup(
                                Rules: new[] { new MondayFilterRule(linkColumnId, MondayFilterOperators.IsEmptyOperator, null) },
                                All: null,
                                Any: null,
                                Not: null)
                        },
                        Not: null))),

            new(
                "Exclude Status Label",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: Array.Empty<MondayFilterRule>(),
                    CreatedAt: null,
                    Condition: new MondayFilterConditionGroup(
                        Rules: null,
                        All: null,
                        Any: null,
                        Not: new MondayFilterConditionGroup(
                            Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel) },
                            All: null,
                            Any: null,
                            Not: null))))
        };

        // Optional created-at bounded version
        cases.Add(new FilterDefinitionCase(
            "Status Equals Label Within 30 Days",
            new MondayFilterDefinition(
                GroupId: null,
                Rules: Array.Empty<MondayFilterRule>(),
                CreatedAt: new DateRangeFilter
                {
                    From = DateTime.UtcNow.AddDays(-30),
                    To = DateTime.UtcNow
                },
                Condition: new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel) },
                    All: null,
                    Any: null,
                    Not: null))));

        cases.Add(new FilterDefinitionCase(
            "Has Matching SubItem Status",
            new MondayFilterDefinition(
                GroupId: null,
                Rules: Array.Empty<MondayFilterRule>(),
                CreatedAt: null,
                Condition: null,
                SubItems: new MondaySubItemFilter(
                    new MondayFilterDefinition(
                        GroupId: null,
                        Rules: new[]
                        {
                            new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel)
                        },
                        CreatedAt: null,
                        Condition: null,
                        SubItems: null,
                        Updates: null),
                    MondayAggregationMode.Any),
                Updates: null)));

        cases.Add(new FilterDefinitionCase(
            "Has Updates",
            new MondayFilterDefinition(
                GroupId: null,
                Rules: Array.Empty<MondayFilterRule>(),
                CreatedAt: null,
                Condition: null,
                SubItems: null,
                Updates: new MondayUpdateFilter(Array.Empty<MondayUpdateRule>(), MondayAggregationMode.Any))));



        var timelineColumnId = await TryResolveColumnIdAsync(config.BoardId, config.TimelineColumnTitle, columnResolver, cancellationToken);
        if (!string.IsNullOrWhiteSpace(timelineColumnId))
        {
            cases.Add(new FilterDefinitionCase(
                "Complex Composite Filter",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.NotEqualsOperator, "Done") },
                    CreatedAt: null,
                    Condition: null,
                    SubItems: new MondaySubItemFilter(
                        new MondayFilterDefinition(
                            GroupId: null,
                            Rules: Array.Empty<MondayFilterRule>(),
                            CreatedAt: null,
                            Condition: new MondayFilterConditionGroup(
                                Rules: null,
                                All: new[]
                                {
                                    new MondayFilterConditionGroup(
                                        Rules: new[]
                                        {
                                            new MondayFilterRule(
                                                timelineColumnId!,
                                                MondayFilterOperators.BeforeOperator,
                                                new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o"),
                                                ValueType: MondayFilterValueTypes.Timeline)
                                        },
                                        All: null,
                                        Any: null,
                                        Not: null),
                                    new MondayFilterConditionGroup(
                                        Rules: null,
                                        All: null,
                                        Any: new[]
                                        {
                                            new MondayFilterConditionGroup(
                                                Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel) },
                                                All: null,
                                                Any: null,
                                                Not: null),
                                            new MondayFilterConditionGroup(
                                                Rules: new[] { new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, "Planned") },
                                                All: null,
                                                Any: null,
                                                Not: null)
                                        },
                                        Not: null)
                                },
                                Any: null,
                                Not: null),
                            SubItems: null,
                            Updates: new MondayUpdateFilter(Array.Empty<MondayUpdateRule>(), MondayAggregationMode.Any),
                            ActivityLogs: new MondayActivityLogFilter(Array.Empty<MondayActivityLogRule>(), MondayAggregationMode.Any)),
                        MondayAggregationMode.Any),
                    Updates: new MondayUpdateFilter(Array.Empty<MondayUpdateRule>(), MondayAggregationMode.Any),
                    ActivityLogs: new MondayActivityLogFilter(Array.Empty<MondayActivityLogRule>(), MondayAggregationMode.Any))));

            cases.Add(new FilterDefinitionCase(
                "SubItem Timeline + Status With Updates",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: Array.Empty<MondayFilterRule>(),
                    CreatedAt: null,
                    Condition: null,
                    SubItems: new MondaySubItemFilter(
                        new MondayFilterDefinition(
                            GroupId: null,
                            Rules: Array.Empty<MondayFilterRule>(),
                            CreatedAt: null,
                            Condition: new MondayFilterConditionGroup(
                                Rules: null,
                                All: new[]
                                {
                                    new MondayFilterConditionGroup(
                                        Rules: new[]
                                        {
                                            new MondayFilterRule(
                                                timelineColumnId!,
                                                MondayFilterOperators.BeforeOperator,
                                                new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o"),
                                                ValueType: MondayFilterValueTypes.Timeline)
                                        },
                                        All: null,
                                        Any: null,
                                        Not: null),
                                    new MondayFilterConditionGroup(
                                        Rules: null,
                                        All: null,
                                        Any: new[]
                                        {
                                            new MondayFilterConditionGroup(
                                                Rules: new[]
                                                {
                                                    new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, statusLabel)
                                                },
                                                All: null,
                                                Any: null,
                                                Not: null),
                                            new MondayFilterConditionGroup(
                                                Rules: new[]
                                                {
                                                    new MondayFilterRule(statusColumnId, MondayFilterOperators.EqualsOperator, "Planned")
                                                },
                                                All: null,
                                                Any: null,
                                                Not: null)
                                        },
                                        Not: null)
                                },
                                Any: null,
                                Not: null),
                            SubItems: null,
                            Updates: new MondayUpdateFilter(Array.Empty<MondayUpdateRule>(), MondayAggregationMode.Any)),
                        MondayAggregationMode.Any),
                    Updates: null)));

            cases.Add(new FilterDefinitionCase(
                "Timeline Ends Before Next Week",
                new MondayFilterDefinition(
                    GroupId: null,
                    Rules: new[]
                    {
                        new MondayFilterRule(
                            timelineColumnId!,
                            MondayFilterOperators.BeforeOperator,
                            DateTime.UtcNow.AddDays(7).ToString("o"),
                            ValueType: MondayFilterValueTypes.Timeline)
                    },
                    CreatedAt: null,
                    Condition: null)));
        }

        return cases;
    }

    private static async Task<string> ResolveColumnIdAsync(
        string boardId,
        string? columnTitle,
        IColumnResolverService resolver,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(columnTitle))
        {
            throw new InvalidOperationException("Column title must be provided in test configuration.");
        }

        return await resolver.ResolveColumnIdAsync(
            boardId,
            columnId: null,
            columnTitle,
            cancellationToken);
    }

    private static async Task<string?> TryResolveColumnIdAsync(
        string boardId,
        string? columnTitle,
        IColumnResolverService resolver,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(columnTitle))
        {
            return null;
        }

        try
        {
            return await resolver.ResolveColumnIdAsync(
                boardId,
                columnId: null,
                columnTitle,
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public sealed record FilterDefinitionCase(
        string Name,
        MondayFilterDefinition Definition);
}



