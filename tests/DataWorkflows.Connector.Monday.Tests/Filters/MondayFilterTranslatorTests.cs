using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using FluentAssertions;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataWorkflows.Connector.Monday.Tests.Filters;

public class MondayFilterTranslatorTests
{
    private readonly MondayFilterTranslator _translator = new(NullLogger<MondayFilterTranslator>.Instance);

    [Fact]
    public void Translate_ShouldReturnNulls_WhenFilterIsNull()
    {
        var result = _translator.Translate(null);

        result.QueryParams.Should().BeNull();
        result.ClientPredicate.Should().BeNull();
        result.SubItemPredicate.Should().BeNull();
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

        result.QueryParams.Should().BeNull();
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
        result.QueryParams.Should().BeNull();
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

        result.QueryParams.Should().BeNull();
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
    public void Translate_ShouldBuildSubItemPredicate_ForAnyMode()
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

        result.SubItemPredicate.Should().NotBeNull();

        var matchingSubItems = new[]
        {
            CreateItem("status", "In Progress"),
            CreateItem("status", "Done")
        };

        var nonMatchingSubItems = new[]
        {
            CreateItem("status", "In Progress")
        };

        result.SubItemPredicate!(matchingSubItems).Should().BeTrue();
        result.SubItemPredicate!(nonMatchingSubItems).Should().BeFalse();
    }

    [Fact]
    public void Translate_ShouldBuildSubItemPredicate_ForAllMode()
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

        result.SubItemPredicate.Should().NotBeNull();

        var allMatching = new[]
        {
            CreateItem("status", "Done"),
            CreateItem("status", "Done")
        };

        var partiallyMatching = new[]
        {
            CreateItem("status", "Done"),
            CreateItem("status", "In Progress")
        };

        var noSubItems = Array.Empty<MondayItemDto>();

        result.SubItemPredicate!(allMatching).Should().BeTrue();
        result.SubItemPredicate!(partiallyMatching).Should().BeFalse();
        result.SubItemPredicate!(noSubItems).Should().BeFalse();
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
}


