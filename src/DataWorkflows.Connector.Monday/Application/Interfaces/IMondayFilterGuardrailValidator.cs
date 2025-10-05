using DataWorkflows.Connector.Monday.Application.Filters;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

/// <summary>
/// Validates Monday filter definitions against configured complexity guardrails.
/// </summary>
public interface IMondayFilterGuardrailValidator
{
    /// <summary>
    /// Validates a filter definition and returns the validation result.
    /// </summary>
    GuardrailValidationResult Validate(MondayFilterDefinition? filterDefinition);
}
