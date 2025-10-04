using System.Collections.Generic;

namespace DataWorkflows.Connector.Monday.Application.Filters;

public sealed record MondayFilterConditionGroup(
    IReadOnlyList<MondayFilterRule>? Rules,
    IReadOnlyList<MondayFilterConditionGroup>? All,
    IReadOnlyList<MondayFilterConditionGroup>? Any,
    MondayFilterConditionGroup? Not)
{
    public bool HasContent =>
        (Rules is { Count: > 0 }) ||
        (All is { Count: > 0 }) ||
        (Any is { Count: > 0 }) ||
        Not is not null;
}
