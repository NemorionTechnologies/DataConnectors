using System.Collections.Generic;
using System.Linq;
using DataWorkflows.Connector.Monday.Application.Filters;

namespace DataWorkflows.Connector.Monday.Application.DTOs;

public class GetItemsFilterModel
{
    public string? GroupId { get; set; }
    public DateRangeFilter? TimelineFilter { get; set; }
    public Dictionary<string, string>? ColumnFilters { get; set; } // Key: ColumnName, Value: ColumnValue

    public MondayFilterDefinition ToFilterDefinition()
    {
        var rules = (ColumnFilters ?? new Dictionary<string, string>())
            .Select(pair => new MondayFilterRule(pair.Key, MondayFilterOperators.EqualsOperator, pair.Value))
            .ToList();

        return new MondayFilterDefinition(
            GroupId,
            rules,
            TimelineFilter,
            Condition: null,
            SubItems: null,
            Updates: null);
    }

    public static MondayFilterDefinition? ToFilterDefinition(GetItemsFilterModel? filter)
        => filter?.ToFilterDefinition();
}
