using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure.ColumnFilterEvaluators;

/// <summary>
/// Evaluates filter conditions for timeline/daterange columns.
/// </summary>
public class TimelineColumnEvaluator : IColumnFilterEvaluator
{
    private readonly IColumnValueParser _columnValueParser;

    public TimelineColumnEvaluator(IColumnValueParser columnValueParser)
    {
        _columnValueParser = columnValueParser;
    }

    public IReadOnlyList<string> SupportedColumnTypes => new[] { "timeline", "date", "daterange" };

    public bool Evaluate(MondayColumnValueDto? columnValue, FilterCondition condition, object? expectedValue)
    {
        var timeline = ParseValue(columnValue) as TimelineValue;

        return condition switch
        {
            FilterCondition.IsNull => timeline == null || (timeline.From == null && timeline.To == null),
            FilterCondition.IsNotNull => timeline != null && (timeline.From != null || timeline.To != null),
            FilterCondition.GreaterThan => CompareTimeline(timeline, expectedValue, (t, e) => t > e),
            FilterCondition.LessThan => CompareTimeline(timeline, expectedValue, (t, e) => t < e),
            FilterCondition.GreaterThanOrEqual => CompareTimeline(timeline, expectedValue, (t, e) => t >= e),
            FilterCondition.LessThanOrEqual => CompareTimeline(timeline, expectedValue, (t, e) => t <= e),
            FilterCondition.Equals => CompareTimeline(timeline, expectedValue, (t, e) => t == e),
            _ => throw new NotSupportedException($"Condition {condition} is not supported for timeline columns")
        };
    }

    public object? ParseValue(MondayColumnValueDto? columnValue)
    {
        return _columnValueParser.ParseTimeline(columnValue);
    }

    private bool CompareTimeline(TimelineValue? timeline, object? expectedValue, Func<DateTime, DateTime, bool> comparer)
    {
        if (timeline?.To == null || expectedValue == null)
        {
            return false;
        }

        // Use timeline end date for comparisons
        var compareDate = expectedValue switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            string s when DateTime.TryParse(s, out var parsed) => parsed,
            _ => throw new ArgumentException($"Cannot compare timeline with type {expectedValue.GetType().Name}")
        };

        return comparer(timeline.To.Value, compareDate);
    }
}
