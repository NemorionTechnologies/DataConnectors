using System;
using System.Linq;
﻿using System;

namespace DataWorkflows.Connector.Monday.Application.Filters;

public sealed record MondayFilterComplexityMetrics(
    int ItemRuleCount,
    int SubItemRuleCount,
    int UpdateRuleCount,
    int ActivityRuleCount,
    int MaxDepth,
    bool HasCompositeRules)
{
    public int TotalRuleCount => ItemRuleCount + SubItemRuleCount + UpdateRuleCount + ActivityRuleCount;

    public override string ToString() =>
        $"Items={ItemRuleCount}, SubItems={SubItemRuleCount}, Updates={UpdateRuleCount}, Activity={ActivityRuleCount}, Depth={MaxDepth}, Composite={HasCompositeRules}";
}

public static class MondayFilterComplexityAnalyzer
{
    public static MondayFilterComplexityMetrics Analyze(MondayFilterDefinition? definition)
    {
        if (definition is null || definition.IsEmpty)
        {
            return new MondayFilterComplexityMetrics(0, 0, 0, 0, 0, false);
        }

        var itemRules = CountRules(definition.Condition) + (definition.Rules?.Count ?? 0);
        var subItemMetrics = AnalyzeSubItem(definition.SubItems);
        var updateRules = CountUpdateRules(definition.Updates);
        var activityRules = CountActivityRules(definition.ActivityLogs);
        var depth = Math.Max(ComputeDepth(definition.Condition), subItemMetrics.Depth);
        var hasComposite = HasComposite(definition.Condition) || subItemMetrics.HasComposite;

        return new MondayFilterComplexityMetrics(
            itemRules,
            subItemMetrics.RuleCount,
            updateRules,
            activityRules,
            depth,
            hasComposite);
    }

    private static (int RuleCount, int Depth, bool HasComposite) AnalyzeSubItem(MondaySubItemFilter? subItem)
    {
        if (subItem is null)
        {
            return (0, 0, false);
        }

        var definition = subItem.Definition;
        var ruleCount = CountRules(definition.Condition) + (definition.Rules?.Count ?? 0);
        var depth = ComputeDepth(definition.Condition) + 1;
        var hasComposite = HasComposite(definition.Condition);
        ruleCount += CountUpdateRules(definition.Updates);
        ruleCount += CountActivityRules(definition.ActivityLogs);

        return (ruleCount, depth, hasComposite);
    }

    private static int CountRules(MondayFilterConditionGroup? group)
    {
        if (group is null || !group.HasContent)
        {
            return 0;
        }

        var count = group.Rules?.Count ?? 0;

        if (group.All is { Count: > 0 })
        {
            count += group.All.Sum(CountRules);
        }

        if (group.Any is { Count: > 0 })
        {
            count += group.Any.Sum(CountRules);
        }

        if (group.Not is not null)
        {
            count += CountRules(group.Not);
        }

        return count;
    }

    private static int CountUpdateRules(MondayUpdateFilter? filter)
    {
        if (filter is null || !filter.HasRules)
        {
            return 0;
        }

        return filter.Rules.Count;
    }

    private static int CountActivityRules(MondayActivityLogFilter? filter)
    {
        if (filter is null || !filter.HasRules)
        {
            return 0;
        }

        return filter.Rules.Count;
    }

    private static int ComputeDepth(MondayFilterConditionGroup? group)
    {
        if (group is null || !group.HasContent)
        {
            return 0;
        }

        var childDepths = new[]
        {
            group.All?.Select(ComputeDepth).DefaultIfEmpty(0).Max() ?? 0,
            group.Any?.Select(ComputeDepth).DefaultIfEmpty(0).Max() ?? 0,
            group.Not is null ? 0 : ComputeDepth(group.Not)
        };

        return 1 + childDepths.Max();
    }

    private static bool HasComposite(MondayFilterConditionGroup? group)
    {
        if (group is null || !group.HasContent)
        {
            return false;
        }

        var directComposite = (group.All?.Count ?? 0) + (group.Any?.Count ?? 0) + (group.Not is null ? 0 : 1) > 1;

        return directComposite ||
            (group.All?.Any(HasComposite) ?? false) ||
            (group.Any?.Any(HasComposite) ?? false) ||
            (group.Not is not null && HasComposite(group.Not));
    }
}
