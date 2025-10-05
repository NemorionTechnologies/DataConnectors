using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using FluentAssertions;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataWorkflows.Connector.Monday.Tests.Filters;

public class MondayFilterTranslatorTests
{
    private readonly MondayFilterTranslator _translator;

    public MondayFilterTranslatorTests()
    {
        var guardrailValidator = CreateMockGuardrailValidator();
        _translator = new MondayFilterTranslator(NullLogger<MondayFilterTranslator>.Instance, guardrailValidator);
    }

    private static IMondayFilterGuardrailValidator CreateMockGuardrailValidator()
    {
        // Mock validator that always passes for unit tests
        return new PassthroughGuardrailValidator();
    }

    private class PassthroughGuardrailValidator : IMondayFilterGuardrailValidator
    {
        public GuardrailValidationResult Validate(MondayFilterDefinition? filterDefinition)
        {
            return GuardrailValidationResult.Ok();
        }
    }

    [Fact]
    public void Translate_ShouldReturnNulls_WhenFilterIsNull()
    {
        var result = _translator.Translate(null);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().BeNull();
        result.SubItemTranslation.Should().BeNull();
        result.UpdatePredicate.Should().BeNull();
        result.ActivityLogPredicate.Should().BeNull();
        result.ComplexityMetrics.TotalRuleCount.Should().Be(0);
        result.ActivityLogPredicate.Should().BeNull();
    }

    [Fact]
    public void Translate_ShouldProvidePredicate_ForEqualsOperator()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        // Now produces server-side query params for simple AND chains
        result.QueryParams.Should().NotBeNull();
        result.ClientPredicate.Should().NotBeNull();

        var matchingItem = CreateItem("status", text: "Done");
        var nonMatchingItem = CreateItem("status", text: "In Progress");

        result.ClientPredicate!(matchingItem).Should().BeTrue();
        result.ClientPredicate!(nonMatchingItem).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldFallbackToPredicate_WhenRuleHasNoValue()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, Value: null) },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();
    }

    [Fact]
    public void Translate_ShouldMatchIsEmpty_WhenColumnMissing()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("link", MondayFilterOperators.IsEmptyOperator, Value: null) },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);
        // isEmpty is server-side translatable
        result.QueryParams.Should().NotBeNull();
        result.ClientPredicate.Should().NotBeNull();

        var itemWithoutColumn = CreateItem("status", text: "Something");
        result.ClientPredicate!(itemWithoutColumn).Should().BeTrue();

        var itemWithValue = CreateItem("link", text: "https://example.com");
        result.ClientPredicate!(itemWithValue).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldRespectCreatedAtRange()
    {
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow.AddDays(-1);
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: new DateRangeFilter { From = from, To = to },
            Condition: null);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();

        var insideRange = CreateItem(createdAt: DateTimeOffset.UtcNow.AddDays(-2));
        var outsideRange = CreateItem(createdAt: DateTimeOffset.UtcNow.AddDays(-10));

        result.ClientPredicate!(insideRange).Should().BeTrue();
        result.ClientPredicate!(outsideRange).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldHandleAllGroup()
    {
        var condition = new MondayFilterConditionGroup(
            Rules: null,
            All: new[]
            {
                new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
                    All: null,
                    Any: null,
                    Not: null),
                new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("type", MondayFilterOperators.EqualsOperator, "Feature") },
                    All: null,
                    Any: null,
                    Not: null)
            },
            Any: null,
            Not: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: condition);

        var result = _translator.Translate(filter);

        // Nested ALL groups with supported operators now translate server-side
        result.QueryParams.Should().NotBeNull();
        result.ClientPredicate.Should().NotBeNull();

        var matchingItem = CreateItem();
        matchingItem.ColumnValues["status"].Text = "Done";
        matchingItem.ColumnValues.Add("type", new MondayColumnValueDto { Id = "type", Text = "Feature" });

        var failingItem = CreateItem();
        failingItem.ColumnValues["status"].Text = "Done";
        failingItem.ColumnValues.Add("type", new MondayColumnValueDto { Id = "type", Text = "Bug" });

        result.ClientPredicate!(matchingItem).Should().BeTrue();
        result.ClientPredicate!(failingItem).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldFallbackForAnyGroup_ButEvaluatePredicate()
    {
        var condition = new MondayFilterConditionGroup(
            Rules: null,
            All: null,
            Any: new[]
            {
                new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("priority", MondayFilterOperators.EqualsOperator, "High") },
                    All: null,
                    Any: null,
                    Not: null),
                new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("priority", MondayFilterOperators.EqualsOperator, "Medium") },
                    All: null,
                    Any: null,
                    Not: null)
            },
            Not: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: condition);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();

        var high = CreateItem("priority", "High");
        var medium = CreateItem("priority", "Medium");
        var low = CreateItem("priority", "Low");

        result.ClientPredicate!(high).Should().BeTrue();
        result.ClientPredicate!(medium).Should().BeTrue();
        result.ClientPredicate!(low).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldFallbackForNotGroup()
    {
        var condition = new MondayFilterConditionGroup(
            Rules: null,
            All: null,
            Any: null,
            Not: new MondayFilterConditionGroup(
                Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
                All: null,
                Any: null,
                Not: null));

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: condition);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();

        var done = CreateItem("status", "Done");
        var wip = CreateItem("status", "Working on it");

        result.ClientPredicate!(done).Should().BeFalse();
        result.ClientPredicate!(wip).Should().BeTrue();
    }

    [Fact]
    public void Translate_ShouldCompareNumbers_GreaterThan()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("effort", MondayFilterOperators.GreaterThanOperator, "5", ValueType: MondayFilterValueTypes.Number)
            },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        var matchingItem = CreateNumericItem("effort", 8);
        var failingItem = CreateNumericItem("effort", 3);

        result.ClientPredicate!(matchingItem).Should().BeTrue();
        result.ClientPredicate!(failingItem).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldCompareDates_Before()
    {
       var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("due", MondayFilterOperators.BeforeOperator, DateTime.UtcNow.AddDays(1).ToString("o"), ValueType: MondayFilterValueTypes.Date)
            },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        var matchingItem = CreateDateItem("due", DateTime.UtcNow);
        var failingItem = CreateDateItem("due", DateTime.UtcNow.AddDays(5));

        result.ClientPredicate!(matchingItem).Should().BeTrue();
        result.ClientPredicate!(failingItem).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldEvaluateTimelineBetween()
    {
        var start = DateTime.UtcNow.AddDays(-7).ToString("o");
        var end = DateTime.UtcNow.AddDays(7).ToString("o");

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule(
                    "timeline",
                    MondayFilterOperators.BetweenOperator,
                    start,
                    SecondValue: end,
                    ValueType: MondayFilterValueTypes.Timeline)
            },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        var overlapping = CreateTimelineItem("timeline", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var nonOverlapping = CreateTimelineItem("timeline", DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(12));

        result.ClientPredicate!(overlapping).Should().BeTrue();
        result.ClientPredicate!(nonOverlapping).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldBuildSubItemTranslation_ForAnyMode()
    {
        var subItemDefinition = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: new MondaySubItemFilter(subItemDefinition, MondayAggregationMode.Any),
            Updates: null);

        var result = _translator.Translate(filter);

        result.SubItemTranslation.Should().NotBeNull();
        result.SubItemTranslation!.Mode.Should().Be(MondayAggregationMode.Any);
        result.SubItemTranslation!.UpdatePredicate.Should().BeNull();
        result.SubItemTranslation!.ItemPredicate!(CreateItem("status", "Done")).Should().BeTrue();
        result.SubItemTranslation!.ItemPredicate!(CreateItem("status", "In Progress")).Should().BeFalse();
    }



    [Fact]
    public void Translate_ShouldBuildSubItemTranslation_ForAllMode()
    {
        var subItemDefinition = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: new MondaySubItemFilter(subItemDefinition, MondayAggregationMode.All),
            Updates: null);

        var result = _translator.Translate(filter);

        result.SubItemTranslation.Should().NotBeNull();
        result.SubItemTranslation!.Mode.Should().Be(MondayAggregationMode.All);
        result.SubItemTranslation!.UpdatePredicate.Should().BeNull();
        result.SubItemTranslation!.ItemPredicate!(CreateItem("status", "Done")).Should().BeTrue();
        result.SubItemTranslation!.ItemPredicate!(CreateItem("status", "In Progress")).Should().BeFalse();
    }



    [Fact]
    public void Translate_ShouldBuildUpdatePredicate_ForAnyMode()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: new MondayUpdateFilter(Array.Empty<MondayUpdateRule>(), MondayAggregationMode.Any));

        var result = _translator.Translate(filter);

        result.UpdatePredicate.Should().NotBeNull();
        result.UpdatePredicate!(new[] { CreateUpdate(body: "Initial update") }).Should().BeTrue();
        result.UpdatePredicate!(Array.Empty<MondayUpdateDto>()).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldBuildUpdatePredicate_ForAllMode()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: new MondayUpdateFilter(
                new[]
                {
                    new MondayUpdateRule(MondayUpdateFields.Body, MondayFilterOperators.ContainsOperator, "done"),
                    new MondayUpdateRule(MondayUpdateFields.CreatorId, MondayFilterOperators.EqualsOperator, "user-1")
                },
                MondayAggregationMode.All));

        var result = _translator.Translate(filter);

        result.UpdatePredicate.Should().NotBeNull();
        result.UpdatePredicate!(new[]
        {
            CreateUpdate(body: "all DONE", creatorId: "user-1"),
            CreateUpdate(body: "Done", creatorId: "user-1")
        }).Should().BeTrue();
        result.UpdatePredicate!(new[]
        {
            CreateUpdate(body: "Done", creatorId: "user-1"),
            CreateUpdate(body: "Done", creatorId: "user-2")
        }).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldBuildActivityLogPredicate_ForAnyMode()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: null,
            ActivityLogs: new MondayActivityLogFilter(Array.Empty<MondayActivityLogRule>(), MondayAggregationMode.Any));

        var result = _translator.Translate(filter);

        result.ActivityLogPredicate.Should().NotBeNull();
        result.ActivityLogPredicate!(new[] { CreateActivityLog() }).Should().BeTrue();
        result.ActivityLogPredicate!(Array.Empty<MondayActivityLogDto>()).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldBuildActivityLogPredicate_ForAllMode()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: null,
            ActivityLogs: new MondayActivityLogFilter(
                new[]
                {
                    new MondayActivityLogRule(MondayActivityLogFields.EventType, MondayFilterOperators.EqualsOperator, "status_changed"),
                    new MondayActivityLogRule(MondayActivityLogFields.UserId, MondayFilterOperators.EqualsOperator, "user-1")
                },
                MondayAggregationMode.All));

        var result = _translator.Translate(filter);

        result.ActivityLogPredicate.Should().NotBeNull();
        result.ActivityLogPredicate!(new[]
        {
            CreateActivityLog(eventType: "status_changed", userId: "user-1"),
            CreateActivityLog(eventType: "status_changed", userId: "user-1")
        }).Should().BeTrue();

        result.ActivityLogPredicate!(new[]
        {
            CreateActivityLog(eventType: "status_changed", userId: "user-1"),
            CreateActivityLog(eventType: "status_changed", userId: "user-2")
        }).Should().BeFalse();
    }


    [Fact]
    public void Translate_ShouldBuildCompositePredicates()
    {
        var cutoff = DateTime.UtcNow.AddDays(-5);

        var subItemDefinition = new MondayFilterDefinition(
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
                                "timeline",
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
                                Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Working on it") },
                                All: null,
                                Any: null,
                                Not: null),
                            new MondayFilterConditionGroup(
                                Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Planned") },
                                All: null,
                                Any: null,
                                Not: null)
                        },
                        Not: null)
                },
                Any: null,
                Not: null),
            SubItems: null,
            Updates: new MondayUpdateFilter(
                new[] { new MondayUpdateRule(MondayUpdateFields.CreatorId, MondayFilterOperators.EqualsOperator, "user-1") },
                MondayAggregationMode.All),
            ActivityLogs: new MondayActivityLogFilter(
                new[] { new MondayActivityLogRule(MondayActivityLogFields.EventType, MondayFilterOperators.ContainsOperator, "status") },
                MondayAggregationMode.Any));

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: new MondayFilterConditionGroup(
                Rules: new[] { new MondayFilterRule("parentStatus", MondayFilterOperators.EqualsOperator, "Target") },
                All: null,
                Any: null,
                Not: null),
            SubItems: new MondaySubItemFilter(subItemDefinition, MondayAggregationMode.Any),
            Updates: new MondayUpdateFilter(
                new[] { new MondayUpdateRule(MondayUpdateFields.Body, MondayFilterOperators.ContainsOperator, "ready") },
                MondayAggregationMode.Any),
            ActivityLogs: new MondayActivityLogFilter(
                new[] { new MondayActivityLogRule(MondayActivityLogFields.CreatedAt, MondayFilterOperators.AfterOperator, cutoff.ToString("o")) },
                MondayAggregationMode.Any));

        var result = _translator.Translate(filter);

        result.ClientPredicate.Should().NotBeNull();
        result.UpdatePredicate.Should().NotBeNull();
        result.ActivityLogPredicate.Should().NotBeNull();
        result.SubItemTranslation.Should().NotBeNull();

        var parent = CreateItem("parentStatus", "Target");
        result.ClientPredicate!(parent).Should().BeTrue();
        result.ClientPredicate!(CreateItem("parentStatus", "Other")).Should().BeFalse();

        var parentUpdates = new[] { CreateUpdate(body: "Ready for launch") };
        result.UpdatePredicate!(parentUpdates).Should().BeTrue();
        result.UpdatePredicate!(new[] { CreateUpdate(body: "Something else") }).Should().BeFalse();

        var parentLogs = new[] { CreateActivityLog(createdAt: DateTimeOffset.UtcNow) };
        result.ActivityLogPredicate!(parentLogs).Should().BeTrue();
        result.ActivityLogPredicate!(new[] { CreateActivityLog(createdAt: DateTimeOffset.UtcNow.AddDays(-10)) }).Should().BeFalse();

        var subTranslation = result.SubItemTranslation!;
        subTranslation.ItemPredicate.Should().NotBeNull();
        subTranslation.UpdatePredicate.Should().NotBeNull();
        subTranslation.ActivityLogPredicate.Should().NotBeNull();
        subTranslation.Mode.Should().Be(MondayAggregationMode.Any);

        var matchingSubItem = CreateTimelineItem("timeline", DateTime.UtcNow.AddDays(-15), DateTime.UtcNow.AddDays(-2));
        matchingSubItem.ColumnValues["status"] = new MondayColumnValueDto { Id = "status", Text = "Working on it" };
        subTranslation.ItemPredicate!(matchingSubItem).Should().BeTrue();

        var failingSubItem = CreateTimelineItem("timeline", new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        failingSubItem.ColumnValues["status"] = new MondayColumnValueDto { Id = "status", Text = "Working on it" };
        subTranslation.ItemPredicate!(failingSubItem).Should().BeFalse();

        var subUpdates = new[] { CreateUpdate(creatorId: "user-1") };
        subTranslation.UpdatePredicate!(subUpdates).Should().BeTrue();
        subTranslation.UpdatePredicate!(new[] { CreateUpdate(creatorId: "user-2") }).Should().BeFalse();

        var subLogs = new[] { CreateActivityLog(eventType: "status_changed") };
        subTranslation.ActivityLogPredicate!(subLogs).Should().BeTrue();
        subTranslation.ActivityLogPredicate!(new[] { CreateActivityLog(eventType: "comment_added") }).Should().BeFalse();

        result.ComplexityMetrics.ItemRuleCount.Should().Be(1);
        result.ComplexityMetrics.SubItemRuleCount.Should().Be(5);
        result.ComplexityMetrics.UpdateRuleCount.Should().Be(1);
        result.ComplexityMetrics.ActivityRuleCount.Should().Be(1);
        result.ComplexityMetrics.TotalRuleCount.Should().Be(8);
        result.ComplexityMetrics.MaxDepth.Should().BeGreaterThanOrEqualTo(2);
        result.ComplexityMetrics.HasCompositeRules.Should().BeTrue();
    }

    [Fact]
    public void Translate_ShouldBuildSubItemTranslation_WithActivityLogPredicate()
    {
        var subItemDefinition = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: null,
            ActivityLogs: new MondayActivityLogFilter(Array.Empty<MondayActivityLogRule>(), MondayAggregationMode.Any));

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: new MondaySubItemFilter(subItemDefinition, MondayAggregationMode.All),
            Updates: null);

        var result = _translator.Translate(filter);

        result.SubItemTranslation.Should().NotBeNull();
        result.SubItemTranslation!.ActivityLogPredicate.Should().NotBeNull();
        result.SubItemTranslation!.Mode.Should().Be(MondayAggregationMode.All);
    }

    [Fact]
    public void Translate_ShouldBuildUpdatePredicate_ForDateFilters()
    {
        var cutoff = DateTime.UtcNow.AddDays(-1);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: new MondayUpdateFilter(
                new[]
                {
                    new MondayUpdateRule(MondayUpdateFields.CreatedAt, MondayFilterOperators.AfterOperator, cutoff.ToString("o"))
                },
                MondayAggregationMode.Any));

        var result = _translator.Translate(filter);

        result.UpdatePredicate.Should().NotBeNull();
        result.UpdatePredicate!(new[] { CreateUpdate(createdAt: DateTimeOffset.UtcNow) }).Should().BeTrue();
        result.UpdatePredicate!(new[] { CreateUpdate(createdAt: cutoff.AddHours(-2)) }).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldBuildSubItemTranslation_WithUpdatePredicate()
    {
        var subItemDefinition = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: new MondayUpdateFilter(Array.Empty<MondayUpdateRule>(), MondayAggregationMode.Any));

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null,
            SubItems: new MondaySubItemFilter(subItemDefinition, MondayAggregationMode.Any),
            Updates: null);

        var result = _translator.Translate(filter);

        result.SubItemTranslation.Should().NotBeNull();
        result.SubItemTranslation!.UpdatePredicate.Should().NotBeNull();
        result.SubItemTranslation!.ActivityLogPredicate.Should().BeNull();
        result.SubItemTranslation!.Mode.Should().Be(MondayAggregationMode.Any);
    }

    private static MondayActivityLogDto CreateActivityLog(string? eventType = null, string? userId = null, DateTimeOffset? createdAt = null, string? itemId = null)
    {
        return new MondayActivityLogDto
        {
            EventType = eventType ?? "status_changed",
            UserId = userId ?? "user-1",
            ItemId = itemId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            EventDataJson = "{}"
        };
    }

    private static MondayUpdateDto CreateUpdate(string? body = null, string? creatorId = null, DateTimeOffset? createdAt = null)
    {
        return new MondayUpdateDto
        {
            Id = Guid.NewGuid().ToString(),
            ItemId = Guid.NewGuid().ToString(),
            BodyText = body ?? string.Empty,
            CreatorId = creatorId ?? "user-1",
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    private static MondayItemDto CreateItem(string columnId = "status", string? text = null, DateTimeOffset? createdAt = null)
    {
        return new MondayItemDto
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Item",
            GroupId = "group",
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ColumnValues = new Dictionary<string, MondayColumnValueDto>
            {
                [columnId] = new MondayColumnValueDto
                {
                    Id = columnId,
                    Text = text,
                    Value = text is null ? null : System.Text.Json.JsonSerializer.Serialize(new { text })
                }
            }
        };
    }

    private static MondayItemDto CreateNumericItem(string columnId, double value)
    {
        return new MondayItemDto
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Numeric",
            GroupId = "group",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ColumnValues = new Dictionary<string, MondayColumnValueDto>
            {
                [columnId] = new MondayColumnValueDto
                {
                    Id = columnId,
                    Text = value.ToString(CultureInfo.InvariantCulture),
                    Value = System.Text.Json.JsonSerializer.Serialize(new { number = value.ToString(CultureInfo.InvariantCulture) })
                }
            }
        };
    }

    private static MondayItemDto CreateDateItem(string columnId, DateTime value)
    {
        var iso = value.ToString("o");
        return new MondayItemDto
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Date",
            GroupId = "group",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ColumnValues = new Dictionary<string, MondayColumnValueDto>
            {
                [columnId] = new MondayColumnValueDto
                {
                    Id = columnId,
                    Text = iso,
                    Value = System.Text.Json.JsonSerializer.Serialize(new { date = iso })
                }
            }
        };
    }

    private static MondayItemDto CreateTimelineItem(string columnId, DateTime? from, DateTime? to)
    {
        var valueJson = System.Text.Json.JsonSerializer.Serialize(new { from = from?.ToString("o"), to = to?.ToString("o") });

        return new MondayItemDto
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Timeline",
            GroupId = "group",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ColumnValues = new Dictionary<string, MondayColumnValueDto>
            {
                [columnId] = new MondayColumnValueDto
                {
                    Id = columnId,
                    Text = null,
                    Value = valueJson
                }
            }
        };
    }

    #region Server-Side Translation Tests

    [Fact]
    public void Translate_ShouldProduceQueryParams_ForSimpleEqualsRule()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().NotBeNull();
        result.QueryParams!.HasRules.Should().BeTrue();
        result.QueryParams.Rules.Should().HaveCount(1);
        result.QueryParams.Rules[0].ColumnId.Should().Be("status");
        result.QueryParams.Rules[0].Operator.Should().Be("any_of");
        result.QueryParams.Rules[0].CompareValue.Should().Be("Done");
        result.QueryParams.Rules[0].RequiresCompareValue.Should().BeTrue();
    }

    [Fact]
    public void Translate_ShouldProduceQueryParams_ForMultipleAndRules()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done"),
                new MondayFilterRule("priority", MondayFilterOperators.EqualsOperator, "High")
            },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().NotBeNull();
        result.QueryParams!.Rules.Should().HaveCount(2);
        result.QueryParams.Rules[0].ColumnId.Should().Be("status");
        result.QueryParams.Rules[1].ColumnId.Should().Be("priority");
    }

    [Fact]
    public void Translate_ShouldProduceQueryParams_ForNestedAllGroups()
    {
        var condition = new MondayFilterConditionGroup(
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
            All: new[]
            {
                new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("priority", MondayFilterOperators.EqualsOperator, "High") },
                    All: null,
                    Any: null,
                    Not: null)
            },
            Any: null,
            Not: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: condition);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().NotBeNull();
        result.QueryParams!.Rules.Should().HaveCount(2);
        result.QueryParams.Rules.Should().Contain(r => r.ColumnId == "status");
        result.QueryParams.Rules.Should().Contain(r => r.ColumnId == "priority");
    }

    [Fact]
    public void Translate_ShouldNotProduceQueryParams_ForOrGroup()
    {
        var condition = new MondayFilterConditionGroup(
            Rules: null,
            All: null,
            Any: new[]
            {
                new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
                    All: null,
                    Any: null,
                    Not: null)
            },
            Not: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: condition);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();
    }

    [Fact]
    public void Translate_ShouldNotProduceQueryParams_ForNotGroup()
    {
        var condition = new MondayFilterConditionGroup(
            Rules: null,
            All: null,
            Any: null,
            Not: new MondayFilterConditionGroup(
                Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, "Done") },
                All: null,
                Any: null,
                Not: null));

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: condition);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();
    }

    [Fact]
    public void Translate_ShouldProduceQueryParams_ForSupportedOperators()
    {
        var supportedOperators = new[]
        {
            (MondayFilterOperators.EqualsOperator, "any_of"),
            (MondayFilterOperators.NotEqualsOperator, "not_any_of"),
            (MondayFilterOperators.ContainsOperator, "contains_text"),
            (MondayFilterOperators.IsEmptyOperator, "is_empty")
        };

        foreach (var (filterOp, mondayOp) in supportedOperators)
        {
            var filter = new MondayFilterDefinition(
                GroupId: null,
                Rules: new[] { new MondayFilterRule("status", filterOp, "Done") },
                CreatedAt: null,
                Condition: null);

            var result = _translator.Translate(filter);

            result.QueryParams.Should().NotBeNull($"operator {filterOp} should be server-side translatable");
            result.QueryParams!.Rules[0].Operator.Should().Be(mondayOp);
        }
    }

    [Fact]
    public void Translate_ShouldNotProduceQueryParams_ForUnsupportedOperators()
    {
        var unsupportedOperators = new[]
        {
            MondayFilterOperators.BeforeOperator,
            MondayFilterOperators.AfterOperator,
            MondayFilterOperators.BetweenOperator,
            MondayFilterOperators.GreaterThanOperator,
            MondayFilterOperators.LessThanOperator
        };

        foreach (var op in unsupportedOperators)
        {
            var filter = new MondayFilterDefinition(
                GroupId: null,
                Rules: new[] { new MondayFilterRule("date", op, "2025-01-01") },
                CreatedAt: null,
                Condition: null);

            var result = _translator.Translate(filter);

            result.QueryParams.Should().BeNull($"operator {op} should fall back to client-side");
            result.ClientPredicate.Should().NotBeNull();
        }
    }

    [Fact]
    public void Translate_ShouldProduceQueryParams_ForIsEmptyWithoutValue()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("link", MondayFilterOperators.IsEmptyOperator, Value: null) },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().NotBeNull();
        result.QueryParams!.Rules[0].Operator.Should().Be("is_empty");
        result.QueryParams!.Rules[0].RequiresCompareValue.Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldNotProduceQueryParams_WhenRuleLacksRequiredValue()
    {
        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", MondayFilterOperators.EqualsOperator, Value: null) },
            CreatedAt: null,
            Condition: null);

        var result = _translator.Translate(filter);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().NotBeNull();
    }

    [Fact]
    public void Translate_ShouldThrowGuardrailException_WhenValidationFails()
    {
        var failingValidator = new FailingGuardrailValidator();
        var translator = new MondayFilterTranslator(NullLogger<MondayFilterTranslator>.Instance, failingValidator);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[] { new MondayFilterRule("status", "eq", "Done") },
            CreatedAt: null,
            Condition: null);

        var act = () => translator.Translate(filter);

        act.Should().Throw<Domain.Exceptions.GuardrailViolationException>();
    }

    private class FailingGuardrailValidator : IMondayFilterGuardrailValidator
    {
        public GuardrailValidationResult Validate(MondayFilterDefinition? filterDefinition)
        {
            return GuardrailValidationResult.Error("Test failure");
        }
    }

    #endregion
}












