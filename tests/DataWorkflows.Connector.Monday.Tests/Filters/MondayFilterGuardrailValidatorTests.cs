using DataWorkflows.Connector.Monday.Application.Filters;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataWorkflows.Connector.Monday.Tests.Filters;

public class MondayFilterGuardrailValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnOk_WhenFilterIsNull()
    {
        var validator = CreateValidator();

        var result = validator.Validate(null);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.WarningMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldReturnOk_WhenFilterIsEmpty()
    {
        var validator = CreateValidator();

        var result = validator.Validate(MondayFilterDefinition.Empty);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.WarningMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldReturnOk_WhenGuardrailsAreDisabled()
    {
        var validator = CreateValidator(options => options.IsEnabled = false);

        var filter = CreateFilterWithDepth(10); // Exceeds default max depth

        var result = validator.Validate(filter);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenDepthExceedsMaximum()
    {
        var validator = CreateValidator(options => options.MaxDepth = 2);

        var filter = CreateFilterWithDepth(3);

        var result = validator.Validate(filter);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("depth");
        result.ErrorMessage.Should().Contain("3");
        result.ErrorMessage.Should().Contain("2");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenRuleCountExceedsMaximum()
    {
        var validator = CreateValidator(options => options.MaxTotalRuleCount = 5);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("col1", "eq", "val1"),
                new MondayFilterRule("col2", "eq", "val2"),
                new MondayFilterRule("col3", "eq", "val3"),
                new MondayFilterRule("col4", "eq", "val4"),
                new MondayFilterRule("col5", "eq", "val5"),
                new MondayFilterRule("col6", "eq", "val6") // 6 rules > max of 5
            },
            CreatedAt: null,
            Condition: null);

        var result = validator.Validate(filter);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("6");
        result.ErrorMessage.Should().Contain("5");
    }

    [Fact]
    public void Validate_ShouldReturnWarning_WhenComplexityExceedsWarningThreshold()
    {
        var validator = CreateValidator(options =>
        {
            options.ComplexityWarningThreshold = 3;
            options.MaxTotalRuleCount = 10;
        });

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("col1", "eq", "val1"),
                new MondayFilterRule("col2", "eq", "val2"),
                new MondayFilterRule("col3", "eq", "val3"),
                new MondayFilterRule("col4", "eq", "val4") // 4 rules > warning threshold of 3, but < max of 10
            },
            CreatedAt: null,
            Condition: null);

        var result = validator.Validate(filter);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.WarningMessage.Should().NotBeNull();
        result.WarningMessage.Should().Contain("complexity");
        result.WarningMessage.Should().Contain("4");
    }

    [Fact]
    public void Validate_ShouldReturnOk_WhenFilterIsWithinAllLimits()
    {
        var validator = CreateValidator();

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("status", "eq", "Done"),
                new MondayFilterRule("priority", "eq", "High")
            },
            CreatedAt: null,
            Condition: null);

        var result = validator.Validate(filter);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.WarningMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldCountSubItemRules_InTotalRuleCount()
    {
        var validator = CreateValidator(options => options.MaxTotalRuleCount = 3);

        var subItemDefinition = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("subCol1", "eq", "val1"),
                new MondayFilterRule("subCol2", "eq", "val2")
            },
            CreatedAt: null,
            Condition: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("col1", "eq", "val1"),
                new MondayFilterRule("col2", "eq", "val2")
            },
            CreatedAt: null,
            Condition: null,
            SubItems: new MondaySubItemFilter(subItemDefinition)); // Total: 4 rules (2 parent + 2 sub-item)

        var result = validator.Validate(filter);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("4");
        result.ErrorMessage.Should().Contain("3");
    }

    [Fact]
    public void Validate_ShouldCountUpdateAndActivityRules_InTotalRuleCount()
    {
        var validator = CreateValidator(options => options.MaxTotalRuleCount = 5);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: new[]
            {
                new MondayFilterRule("col1", "eq", "val1"),
                new MondayFilterRule("col2", "eq", "val2")
            },
            CreatedAt: null,
            Condition: null,
            SubItems: null,
            Updates: new MondayUpdateFilter(
                new[]
                {
                    new MondayUpdateRule("body", "contains", "done"),
                    new MondayUpdateRule("creatorId", "eq", "user-1")
                }),
            ActivityLogs: new MondayActivityLogFilter(
                new[]
                {
                    new MondayActivityLogRule("eventType", "eq", "status_changed"),
                    new MondayActivityLogRule("userId", "eq", "user-1")
                })); // Total: 6 rules (2 parent + 2 update + 2 activity)

        var result = validator.Validate(filter);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("6");
        result.ErrorMessage.Should().Contain("5");
    }

    [Fact]
    public void Validate_ShouldCalculateDepth_IncludingNestedGroups()
    {
        var validator = CreateValidator(options => options.MaxDepth = 2);

        // Depth 3: Root -> All[0] -> All[0]
        var deeplyNested = new MondayFilterConditionGroup(
            Rules: null,
            All: new[]
            {
                new MondayFilterConditionGroup(
                    Rules: null,
                    All: new[]
                    {
                        new MondayFilterConditionGroup(
                            Rules: new[] { new MondayFilterRule("col", "eq", "val") },
                            All: null,
                            Any: null,
                            Not: null)
                    },
                    Any: null,
                    Not: null)
            },
            Any: null,
            Not: null);

        var filter = new MondayFilterDefinition(
            GroupId: null,
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: deeplyNested);

        var result = validator.Validate(filter);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("depth");
        result.ErrorMessage.Should().Contain("3");
        result.ErrorMessage.Should().Contain("2");
    }

    private static MondayFilterGuardrailValidator CreateValidator(Action<GuardrailOptions>? configure = null)
    {
        var options = new GuardrailOptions();
        configure?.Invoke(options);

        var optionsWrapper = Options.Create(options);
        return new MondayFilterGuardrailValidator(optionsWrapper, NullLogger<MondayFilterGuardrailValidator>.Instance);
    }

    private static MondayFilterDefinition CreateFilterWithDepth(int depth)
    {
        if (depth <= 0)
        {
            throw new ArgumentException("Depth must be positive", nameof(depth));
        }

        MondayFilterConditionGroup? BuildNestedGroup(int remainingDepth)
        {
            if (remainingDepth == 1)
            {
                return new MondayFilterConditionGroup(
                    Rules: new[] { new MondayFilterRule("col", "eq", "val") },
                    All: null,
                    Any: null,
                    Not: null);
            }

            return new MondayFilterConditionGroup(
                Rules: null,
                All: new[] { BuildNestedGroup(remainingDepth - 1)! },
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
