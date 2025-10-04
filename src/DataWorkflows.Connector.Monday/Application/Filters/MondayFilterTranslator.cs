using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Connector.Monday.Application.Filters;

public class MondayFilterTranslator : IMondayFilterTranslator
{
    private readonly ILogger<MondayFilterTranslator> _logger;

    public MondayFilterTranslator(ILogger<MondayFilterTranslator> logger)
    {
        _logger = logger;
    }

    public MondayFilterTranslationResult Translate(MondayFilterDefinition? filterDefinition)
    {
        if (filterDefinition is null || filterDefinition.IsEmpty)
        {
            return new MondayFilterTranslationResult(null, null, null);
        }

        var rootCondition = BuildRootCondition(filterDefinition);
        var predicate = BuildPredicate(rootCondition, filterDefinition.CreatedAt);
        var subItemPredicate = BuildSubItemPredicate(filterDefinition.SubItems);

        if (predicate is null && subItemPredicate is null)
        {
            _logger.LogDebug("Filter definition produced no executable predicate.");
        }

        return new MondayFilterTranslationResult(null, predicate, subItemPredicate);
    }

    private static MondayFilterConditionGroup? BuildRootCondition(MondayFilterDefinition definition)
    {
        var hasRules = definition.Rules is { Count: > 0 };
        var condition = definition.Condition;

        if (condition is null)
        {
            return hasRules ? new MondayFilterConditionGroup(definition.Rules, null, null, null) : null;
        }

        if (!hasRules)
        {
            return condition;
        }

        // Combine legacy rules with explicit condition via implicit AND
        return new MondayFilterConditionGroup(
            definition.Rules,
            All: new[] { condition },
            Any: null,
            Not: null);
    }

    private static Func<MondayItemDto, bool>? BuildPredicate(
        MondayFilterConditionGroup? condition,
        DateRangeFilter? createdAtRange)
    {
        var hasCreatedAt = createdAtRange is not null;
        var hasCondition = condition?.HasContent ?? false;

        if (!hasCreatedAt && !hasCondition)
        {
            return null;
        }

        return item =>
        {
            if (hasCreatedAt)
            {
                var from = DateTime.SpecifyKind(createdAtRange!.From, DateTimeKind.Utc);
                var to = DateTime.SpecifyKind(createdAtRange.To, DateTimeKind.Utc);
                var createdAt = item.CreatedAt.UtcDateTime;

                if (createdAt < from || createdAt > to)
                {
                    return false;
                }
            }

            if (hasCondition && !EvaluateGroup(condition!, item))
            {
                return false;
            }

            return true;
        };
    }

    private static Func<IEnumerable<MondayItemDto>, bool>? BuildSubItemPredicate(MondaySubItemFilter? subItemFilter)
    {
        if (subItemFilter is null)
        {
            return null;
        }

        var childCondition = BuildRootCondition(subItemFilter.Definition);
        var childPredicate = BuildPredicate(childCondition, subItemFilter.Definition.CreatedAt);

        return subItemFilter.Mode switch
        {
            MondayAggregationMode.All => subItems =>
            {
                if (subItems is null)
                {
                    return false;
                }

                var materialized = subItems as ICollection<MondayItemDto> ?? subItems.ToList();

                if (materialized.Count == 0)
                {
                    return false;
                }

                if (childPredicate is null)
                {
                    return true;
                }

                return materialized.All(childPredicate);
            },
            _ => subItems =>
            {
                if (subItems is null)
                {
                    return false;
                }

                var materialized = subItems as ICollection<MondayItemDto> ?? subItems.ToList();

                if (materialized.Count == 0)
                {
                    return false;
                }

                if (childPredicate is null)
                {
                    return true;
                }

                return materialized.Any(childPredicate);
            }
        };
    }

    private static bool EvaluateGroup(MondayFilterConditionGroup? group, MondayItemDto item)
    {
        if (group is null || !group.HasContent)
        {
            return true;
        }

        if (group.Rules is { Count: > 0 } && !group.Rules.All(rule => EvaluateRule(item, rule)))
        {
            return false;
        }

        if (group.All is { Count: > 0 } && !group.All.All(child => EvaluateGroup(child, item)))
        {
            return false;
        }

        if (group.Any is { Count: > 0 } && !group.Any.Any(child => EvaluateGroup(child, item)))
        {
            return false;
        }

        if (group.Not is not null && EvaluateGroup(group.Not, item))
        {
            return false;
        }

        return true;
    }

    private static bool EvaluateRule(MondayItemDto item, MondayFilterRule rule)
    {
        if (!item.ColumnValues.TryGetValue(rule.ColumnId, out var columnValue) || columnValue is null)
        {
            return string.Equals(rule.Operator, MondayFilterOperators.IsEmptyOperator, StringComparison.OrdinalIgnoreCase);
        }

        switch (rule.Operator)
        {
            case MondayFilterOperators.EqualsOperator:
                return CompareText(columnValue, rule.Value, (source, target) => string.Equals(source, target, StringComparison.OrdinalIgnoreCase));
            case MondayFilterOperators.NotEqualsOperator:
                return !CompareText(columnValue, rule.Value, (source, target) => string.Equals(source, target, StringComparison.OrdinalIgnoreCase));
            case MondayFilterOperators.ContainsOperator:
                return CompareText(columnValue, rule.Value, (source, target) => source?.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0);
            case MondayFilterOperators.IsEmptyOperator:
                return string.IsNullOrWhiteSpace(columnValue.Text) && string.IsNullOrWhiteSpace(columnValue.Value);
            case MondayFilterOperators.GreaterThanOperator:
                return EvaluateNumeric(columnValue, rule, (a, b) => a > b) || EvaluateDate(columnValue, rule, (a, b) => a > b);
            case MondayFilterOperators.GreaterThanOrEqualOperator:
                return EvaluateNumeric(columnValue, rule, (a, b) => a >= b) || EvaluateDate(columnValue, rule, (a, b) => a >= b);
            case MondayFilterOperators.LessThanOperator:
                return EvaluateNumeric(columnValue, rule, (a, b) => a < b) || EvaluateDate(columnValue, rule, (a, b) => a < b);
            case MondayFilterOperators.LessThanOrEqualOperator:
                return EvaluateNumeric(columnValue, rule, (a, b) => a <= b) || EvaluateDate(columnValue, rule, (a, b) => a <= b);
            case MondayFilterOperators.BeforeOperator:
                return EvaluateBefore(columnValue, rule);
            case MondayFilterOperators.AfterOperator:
                return EvaluateAfter(columnValue, rule);
            case MondayFilterOperators.BetweenOperator:
                return EvaluateBetween(columnValue, rule);
            default:
                return true;
        }
    }

    private static bool CompareText(
        MondayColumnValueDto columnValue,
        string? comparison,
        Func<string?, string, bool> comparator)
    {
        if (comparison is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(columnValue.Text) && comparator(columnValue.Text, comparison))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(columnValue.Value) && comparator(columnValue.Value, comparison))
        {
            return true;
        }

        return false;
    }

    private static bool EvaluateNumeric(
        MondayColumnValueDto columnValue,
        MondayFilterRule rule,
        Func<double, double, bool> comparator)
    {
        if (!TryParseDouble(rule.Value, out var target))
        {
            return false;
        }

        if (!TryGetNumeric(columnValue, out var actual))
        {
            return false;
        }

        return comparator(actual, target);
    }

    private static bool EvaluateDate(
        MondayColumnValueDto columnValue,
        MondayFilterRule rule,
        Func<DateTime, DateTime, bool> comparator)
    {
        if (!TryParseDate(rule.Value, out var target))
        {
            return false;
        }

        if (!TryGetDate(columnValue, out var actual))
        {
            return false;
        }

        return comparator(actual, target);
    }

    private static bool EvaluateBefore(MondayColumnValueDto columnValue, MondayFilterRule rule)
    {
        if (!TryParseDate(rule.Value, out var target))
        {
            return false;
        }

        if (string.Equals(rule.ValueType, MondayFilterValueTypes.Timeline, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetTimelineRange(columnValue, out var from, out var to) || !to.HasValue)
            {
                return false;
            }

            return to.Value <= target;
        }

        if (!TryGetDate(columnValue, out var actual))
        {
            return false;
        }

        return actual <= target;
    }

    private static bool EvaluateAfter(MondayColumnValueDto columnValue, MondayFilterRule rule)
    {
        if (!TryParseDate(rule.Value, out var target))
        {
            return false;
        }

        if (string.Equals(rule.ValueType, MondayFilterValueTypes.Timeline, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetTimelineRange(columnValue, out var from, out var to) || !from.HasValue)
            {
                return false;
            }

            return from.Value >= target;
        }

        if (!TryGetDate(columnValue, out var actual))
        {
            return false;
        }

        return actual >= target;
    }

    private static bool EvaluateBetween(MondayColumnValueDto columnValue, MondayFilterRule rule)
    {
        if (!TryParseDate(rule.Value, out var start) || !TryParseDate(rule.SecondValue, out var end))
        {
            return false;
        }

        if (string.Equals(rule.ValueType, MondayFilterValueTypes.Timeline, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetTimelineRange(columnValue, out var from, out var to))
            {
                return false;
            }

            var actualFrom = from ?? DateTime.MinValue;
            var actualTo = to ?? DateTime.MaxValue;

            return actualFrom <= end && actualTo >= start;
        }

        if (!TryGetDate(columnValue, out var actual))
        {
            return false;
        }

        return actual >= start && actual <= end;
    }

    private static bool TryGetNumeric(MondayColumnValueDto columnValue, out double actual)
    {
        if (TryParseDouble(columnValue.Text, out actual))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(columnValue.Value))
        {
            try
            {
                using var doc = JsonDocument.Parse(columnValue.Value);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Number && root.TryGetDouble(out actual))
                {
                    return true;
                }

                if (root.TryGetProperty("number", out var numberProperty) && TryParseDouble(numberProperty.GetString(), out actual))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // ignore invalid payloads
            }
        }

        actual = default;
        return false;
    }

    private static bool TryGetDate(MondayColumnValueDto columnValue, out DateTime actual)
    {
        if (TryParseDate(columnValue.Text, out actual))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(columnValue.Value))
        {
            try
            {
                using var doc = JsonDocument.Parse(columnValue.Value);
                var root = doc.RootElement;
                if (root.TryGetProperty("date", out var dateProperty) && TryParseDate(dateProperty.GetString(), out actual))
                {
                    return true;
                }

                if (root.ValueKind == JsonValueKind.String && TryParseDate(root.GetString(), out actual))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        actual = default;
        return false;
    }

    private static bool TryGetTimelineRange(
        MondayColumnValueDto columnValue,
        out DateTime? from,
        out DateTime? to)
    {
        from = null;
        to = null;

        if (string.IsNullOrWhiteSpace(columnValue.Value))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(columnValue.Value);
            var root = doc.RootElement;

            if (root.TryGetProperty("from", out var fromProperty) && TryParseDate(fromProperty.GetString(), out var parsedFrom))
            {
                from = parsedFrom;
            }

            if (root.TryGetProperty("to", out var toProperty) && TryParseDate(toProperty.GetString(), out var parsedTo))
            {
                to = parsedTo;
            }

            return from.HasValue || to.HasValue;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDate(string? value, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return false;
    }
}

public static class MondayFilterOperators
{
    public const string EqualsOperator = "eq";
    public const string NotEqualsOperator = "neq";
    public const string ContainsOperator = "contains";
    public const string IsEmptyOperator = "isEmpty";
    public const string GreaterThanOperator = "gt";
    public const string GreaterThanOrEqualOperator = "gte";
    public const string LessThanOperator = "lt";
    public const string LessThanOrEqualOperator = "lte";
    public const string BeforeOperator = "before";
    public const string AfterOperator = "after";
    public const string BetweenOperator = "between";
}
