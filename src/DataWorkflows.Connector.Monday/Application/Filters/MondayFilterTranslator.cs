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
            return new MondayFilterTranslationResult(null, null, null, null, null);
        }

        var rootCondition = BuildRootCondition(filterDefinition);
        var predicate = BuildPredicate(rootCondition, filterDefinition.CreatedAt);
        var subItemTranslation = BuildSubItemTranslation(filterDefinition.SubItems);
        var updatePredicate = BuildUpdatePredicate(filterDefinition.Updates);
        var activityPredicate = BuildActivityLogPredicate(filterDefinition.ActivityLogs);

        if (predicate is null && subItemTranslation is null && updatePredicate is null && activityPredicate is null)
        {
            _logger.LogDebug("Filter definition produced no executable predicate.");
        }

        return new MondayFilterTranslationResult(null, predicate, subItemTranslation, updatePredicate, activityPredicate);
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

    private static MondaySubItemFilterTranslation? BuildSubItemTranslation(MondaySubItemFilter? subItemFilter)
    {
        if (subItemFilter is null)
        {
            return null;
        }

        var childCondition = BuildRootCondition(subItemFilter.Definition);
        var childPredicate = BuildPredicate(childCondition, subItemFilter.Definition.CreatedAt);
        var childUpdatePredicate = BuildUpdatePredicate(subItemFilter.Definition.Updates);
        var childActivityPredicate = BuildActivityLogPredicate(subItemFilter.Definition.ActivityLogs);

        if (childPredicate is null && childUpdatePredicate is null && childActivityPredicate is null)
        {
            childPredicate = _ => true;
        }

        return new MondaySubItemFilterTranslation(childPredicate, childUpdatePredicate, childActivityPredicate, subItemFilter.Mode);
    }


    private static Func<IEnumerable<MondayActivityLogDto>, bool>? BuildActivityLogPredicate(MondayActivityLogFilter? activityLogFilter)
    {
        if (activityLogFilter is null)
        {
            return null;
        }

        var rulePredicate = BuildActivityLogRulePredicate(activityLogFilter.Rules);

        return activityLogFilter.Mode switch
        {
            MondayAggregationMode.All => logs =>
            {
                if (logs is null)
                {
                    return false;
                }

                var materialized = logs as IList<MondayActivityLogDto> ?? logs.ToList();

                if (materialized.Count == 0)
                {
                    return false;
                }

                if (rulePredicate is null)
                {
                    return true;
                }

                return materialized.All(rulePredicate);
            },
            _ => logs =>
            {
                if (logs is null)
                {
                    return false;
                }

                var materialized = logs as IList<MondayActivityLogDto> ?? logs.ToList();

                if (materialized.Count == 0)
                {
                    return false;
                }

                if (rulePredicate is null)
                {
                    return true;
                }

                return materialized.Any(rulePredicate);
            }
        };
    }

    private static Func<MondayActivityLogDto, bool>? BuildActivityLogRulePredicate(IReadOnlyList<MondayActivityLogRule>? rules)
    {
        if (rules is not { Count: > 0 })
        {
            return null;
        }

        return log => rules.All(rule => EvaluateActivityLogRule(log, rule));
    }

    private static bool EvaluateActivityLogRule(MondayActivityLogDto log, MondayActivityLogRule rule)
    {
        switch (rule.Field)
        {
            case var field when string.Equals(field, MondayActivityLogFields.EventType, StringComparison.OrdinalIgnoreCase):
                return EvaluateTextRule(log.EventType, rule);
            case var field when string.Equals(field, MondayActivityLogFields.UserId, StringComparison.OrdinalIgnoreCase):
                return EvaluateTextRule(log.UserId, rule);
            case var field when string.Equals(field, MondayActivityLogFields.CreatedAt, StringComparison.OrdinalIgnoreCase):
                return EvaluateActivityDate(log.CreatedAt, rule);
            default:
                return true;
        }
    }

    private static bool EvaluateActivityDate(DateTimeOffset actual, MondayActivityLogRule rule)
    {
        var op = rule.Operator;
        if (string.Equals(op, MondayFilterOperators.BetweenOperator, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseDate(rule.Value, out var start) || !TryParseDate(rule.SecondValue, out var end))
            {
                return false;
            }

            var (min, max) = start <= end ? (start, end) : (end, start);
            var actualUtc = actual.UtcDateTime;
            return actualUtc >= min && actualUtc <= max;
        }

        if (!TryParseDate(rule.Value, out var target))
        {
            return false;
        }

        var actualDate = actual.UtcDateTime;

        return op switch
        {
            var o when string.Equals(o, MondayFilterOperators.EqualsOperator, StringComparison.OrdinalIgnoreCase) => actualDate == target,
            var o when string.Equals(o, MondayFilterOperators.GreaterThanOperator, StringComparison.OrdinalIgnoreCase) => actualDate > target,
            var o when string.Equals(o, MondayFilterOperators.GreaterThanOrEqualOperator, StringComparison.OrdinalIgnoreCase) => actualDate >= target,
            var o when string.Equals(o, MondayFilterOperators.LessThanOperator, StringComparison.OrdinalIgnoreCase) => actualDate < target,
            var o when string.Equals(o, MondayFilterOperators.LessThanOrEqualOperator, StringComparison.OrdinalIgnoreCase) => actualDate <= target,
            var o when string.Equals(o, MondayFilterOperators.BeforeOperator, StringComparison.OrdinalIgnoreCase) => actualDate <= target,
            var o when string.Equals(o, MondayFilterOperators.AfterOperator, StringComparison.OrdinalIgnoreCase) => actualDate >= target,
            _ => false
        };
    }

    private static bool EvaluateTextRule(string actual, MondayActivityLogRule rule)
    {
        actual ??= string.Empty;
        var comparison = rule.Value ?? string.Empty;

        return rule.Operator switch
        {
            var op when string.Equals(op, MondayFilterOperators.EqualsOperator, StringComparison.OrdinalIgnoreCase) =>
                string.Equals(actual, comparison, StringComparison.OrdinalIgnoreCase),
            var op when string.Equals(op, MondayFilterOperators.NotEqualsOperator, StringComparison.OrdinalIgnoreCase) =>
                !string.Equals(actual, comparison, StringComparison.OrdinalIgnoreCase),
            var op when string.Equals(op, MondayFilterOperators.ContainsOperator, StringComparison.OrdinalIgnoreCase) =>
                actual.IndexOf(comparison, StringComparison.OrdinalIgnoreCase) >= 0,
            var op when string.Equals(op, MondayFilterOperators.IsEmptyOperator, StringComparison.OrdinalIgnoreCase) =>
                string.IsNullOrWhiteSpace(actual),
            _ => false
        };
    }
    private static Func<IEnumerable<MondayUpdateDto>, bool>? BuildUpdatePredicate(MondayUpdateFilter? updateFilter)
    {
        if (updateFilter is null)
        {
            return null;
        }

        var updateRulePredicate = BuildUpdateRulePredicate(updateFilter.Rules);

        return updateFilter.Mode switch
        {
            MondayAggregationMode.All => updates =>
            {
                if (updates is null)
                {
                    return false;
                }

                var materialized = updates as ICollection<MondayUpdateDto> ?? updates.ToList();

                if (materialized.Count == 0)
                {
                    return false;
                }

                if (updateRulePredicate is null)
                {
                    return true;
                }

                return materialized.All(updateRulePredicate);
            },
            _ => updates =>
            {
                if (updates is null)
                {
                    return false;
                }

                var materialized = updates as ICollection<MondayUpdateDto> ?? updates.ToList();

                if (materialized.Count == 0)
                {
                    return false;
                }

                if (updateRulePredicate is null)
                {
                    return true;
                }

                return materialized.Any(updateRulePredicate);
            }
        };
    }

    private static Func<MondayUpdateDto, bool>? BuildUpdateRulePredicate(IReadOnlyList<MondayUpdateRule>? rules)
    {
        if (rules is not { Count: > 0 })
        {
            return null;
        }

        return update => rules.All(rule => EvaluateUpdateRule(update, rule));
    }

    private static bool EvaluateUpdateRule(MondayUpdateDto update, MondayUpdateRule rule)
    {
        if (rule is null)
        {
            return true;
        }

        var field = rule.Field ?? string.Empty;

        if (string.Equals(field, MondayUpdateFields.Body, StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateUpdateText(update.BodyText, rule);
        }

        if (string.Equals(field, MondayUpdateFields.CreatorId, StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateUpdateText(update.CreatorId, rule);
        }

        if (string.Equals(field, MondayUpdateFields.CreatedAt, StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateUpdateDate(update.CreatedAt, rule);
        }

        return true;
    }

    private static bool EvaluateUpdateText(string? actual, MondayUpdateRule rule)
    {
        actual ??= string.Empty;

        return rule.Operator switch
        {
            var op when string.Equals(op, MondayFilterOperators.EqualsOperator, StringComparison.OrdinalIgnoreCase)
                => !string.IsNullOrWhiteSpace(rule.Value) && string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase),
            var op when string.Equals(op, MondayFilterOperators.NotEqualsOperator, StringComparison.OrdinalIgnoreCase)
                => !string.IsNullOrWhiteSpace(rule.Value) && !string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase),
            var op when string.Equals(op, MondayFilterOperators.ContainsOperator, StringComparison.OrdinalIgnoreCase)
                => !string.IsNullOrWhiteSpace(rule.Value) && actual.IndexOf(rule.Value, StringComparison.OrdinalIgnoreCase) >= 0,
            var op when string.Equals(op, MondayFilterOperators.IsEmptyOperator, StringComparison.OrdinalIgnoreCase)
                => string.IsNullOrWhiteSpace(actual),
            _ => false
        };
    }

    private static bool EvaluateUpdateDate(DateTimeOffset actual, MondayUpdateRule rule)
    {
        var op = rule.Operator;

        if (string.Equals(op, MondayFilterOperators.BetweenOperator, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseDate(rule.Value, out var start) || !TryParseDate(rule.SecondValue, out var end))
            {
                return false;
            }

            var (min, max) = start <= end ? (start, end) : (end, start);
            var utcActual = actual.UtcDateTime;
            return utcActual >= min && utcActual <= max;
        }

        if (!TryParseDate(rule.Value, out var target))
        {
            return false;
        }

        var actualUtc = actual.UtcDateTime;

        if (string.Equals(op, MondayFilterOperators.EqualsOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc == target;
        }

        if (string.Equals(op, MondayFilterOperators.GreaterThanOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc > target;
        }

        if (string.Equals(op, MondayFilterOperators.GreaterThanOrEqualOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc >= target;
        }

        if (string.Equals(op, MondayFilterOperators.LessThanOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc < target;
        }

        if (string.Equals(op, MondayFilterOperators.LessThanOrEqualOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc <= target;
        }

        if (string.Equals(op, MondayFilterOperators.BeforeOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc <= target;
        }

        if (string.Equals(op, MondayFilterOperators.AfterOperator, StringComparison.OrdinalIgnoreCase))
        {
            return actualUtc >= target;
        }

        return false;
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










