using System;
using System.Collections.Generic;
using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Filters;

/// <summary>
/// Represents the filter structure supported for querying Monday items.
/// </summary>
public sealed record MondayFilterDefinition(
    string? GroupId,
    IReadOnlyList<MondayFilterRule> Rules,
    DateRangeFilter? CreatedAt,
    MondayFilterConditionGroup? Condition,
    MondaySubItemFilter? SubItems = null,
    MondayUpdateFilter? Updates = null,
    MondayActivityLogFilter? ActivityLogs = null)
{
    public static readonly MondayFilterDefinition Empty =
        new(null, Array.Empty<MondayFilterRule>(), null, null, null, null, null);

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(GroupId) &&
        (Rules is not { Count: > 0 }) &&
        CreatedAt is null &&
        (Condition is null || !Condition.HasContent) &&
        SubItems is null &&
        Updates is null &&
        ActivityLogs is null;
}

public enum MondayAggregationMode
{
    Any,
    All
}

public sealed record MondaySubItemFilter(
    MondayFilterDefinition Definition,
    MondayAggregationMode Mode = MondayAggregationMode.Any);

public sealed record MondayUpdateFilter(
    IReadOnlyList<MondayUpdateRule> Rules,
    MondayAggregationMode Mode = MondayAggregationMode.Any)
{
    public bool HasRules => Rules is { Count: > 0 };
}

public sealed record MondayUpdateRule(
    string Field,
    string Operator,
    string? Value,
    string? SecondValue = null,
    string? ValueType = null);

public static class MondayUpdateFields
{
    public const string Body = "body";
    public const string CreatorId = "creatorId";
    public const string CreatedAt = "createdAt";
}


public sealed record MondayActivityLogFilter(
    IReadOnlyList<MondayActivityLogRule> Rules,
    MondayAggregationMode Mode = MondayAggregationMode.Any)
{
    public bool HasRules => Rules is { Count: > 0 };
}

public sealed record MondayActivityLogRule(
    string Field,
    string Operator,
    string? Value,
    string? SecondValue = null);

public static class MondayActivityLogFields
{
    public const string EventType = "eventType";
    public const string UserId = "userId";
    public const string CreatedAt = "createdAt";
}

