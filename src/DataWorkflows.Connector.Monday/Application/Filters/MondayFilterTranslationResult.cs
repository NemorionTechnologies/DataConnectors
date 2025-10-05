using System;
using System.Collections.Generic;
using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Filters;

/// <summary>
/// Result of translating a Monday filter definition into server and client-side artifacts.
/// </summary>
public sealed record MondayFilterTranslationResult(
    MondayQueryParams? QueryParams,
    Func<MondayItemDto, bool>? ClientPredicate,
    MondaySubItemFilterTranslation? SubItemTranslation,
    Func<IEnumerable<MondayUpdateDto>, bool>? UpdatePredicate,
    Func<IEnumerable<MondayActivityLogDto>, bool>? ActivityLogPredicate,
    MondayFilterComplexityMetrics ComplexityMetrics);

public sealed record MondaySubItemFilterTranslation(
    Func<MondayItemDto, bool>? ItemPredicate,
    Func<IEnumerable<MondayUpdateDto>, bool>? UpdatePredicate,
    Func<IEnumerable<MondayActivityLogDto>, bool>? ActivityLogPredicate,
    MondayAggregationMode Mode);

/// <summary>
/// Represents the server-side query parameters to send to Monday GraphQL.
/// </summary>
public sealed record MondayQueryParams(IReadOnlyList<MondayQueryRule> Rules)
{
    public bool HasRules => Rules.Count > 0;
}

/// <summary>
/// Represents a single rule inside Monday GraphQL query parameters.
/// </summary>
public sealed record MondayQueryRule(
    string ColumnId,
    string Operator,
    string? CompareValue,
    bool RequiresCompareValue);


