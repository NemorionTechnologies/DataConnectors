using DataWorkflows.Connector.Monday.Application.Interfaces;

namespace DataWorkflows.Connector.Monday.Infrastructure.ColumnFilterEvaluators;

/// <summary>
/// Factory for getting the appropriate column filter evaluator based on column type.
/// </summary>
public class ColumnFilterEvaluatorFactory
{
    private readonly IEnumerable<IColumnFilterEvaluator> _evaluators;
    private readonly TextColumnEvaluator _fallbackEvaluator;

    public ColumnFilterEvaluatorFactory(IEnumerable<IColumnFilterEvaluator> evaluators)
    {
        _evaluators = evaluators;
        _fallbackEvaluator = evaluators.OfType<TextColumnEvaluator>().FirstOrDefault()
            ?? throw new InvalidOperationException("TextColumnEvaluator must be registered as the fallback evaluator");
    }

    /// <summary>
    /// Gets the appropriate evaluator for a column type.
    /// Falls back to TextColumnEvaluator if no specific evaluator is found.
    /// </summary>
    public IColumnFilterEvaluator GetEvaluator(string columnType)
    {
        var evaluator = _evaluators.FirstOrDefault(e =>
            e.SupportedColumnTypes.Contains(columnType, StringComparer.OrdinalIgnoreCase));

        return evaluator ?? _fallbackEvaluator;
    }
}
