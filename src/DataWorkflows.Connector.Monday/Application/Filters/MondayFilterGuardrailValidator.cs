using DataWorkflows.Connector.Monday.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataWorkflows.Connector.Monday.Application.Filters;

/// <summary>
/// Validates Monday filter definitions against configured complexity guardrails.
/// Implements Single Responsibility Principle by focusing solely on validation logic.
/// </summary>
public sealed class MondayFilterGuardrailValidator : IMondayFilterGuardrailValidator
{
    private readonly GuardrailOptions _options;
    private readonly ILogger<MondayFilterGuardrailValidator> _logger;

    public MondayFilterGuardrailValidator(
        IOptions<GuardrailOptions> options,
        ILogger<MondayFilterGuardrailValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public GuardrailValidationResult Validate(MondayFilterDefinition? filterDefinition)
    {
        if (filterDefinition is null || filterDefinition.IsEmpty)
        {
            return GuardrailValidationResult.Ok();
        }

        if (!_options.IsEnabled)
        {
            _logger.LogDebug("Guardrails are disabled; skipping validation.");
            return GuardrailValidationResult.Ok();
        }

        var metrics = MondayFilterComplexityAnalyzer.Analyze(filterDefinition);

        // Hard limit: Max depth
        if (metrics.MaxDepth > _options.MaxDepth)
        {
            var message = $"Filter depth ({metrics.MaxDepth}) exceeds maximum allowed ({_options.MaxDepth})";
            _logger.LogWarning("Guardrail violation: {Message}", message);
            return GuardrailValidationResult.Error(message);
        }

        // Hard limit: Max total rule count
        if (metrics.TotalRuleCount > _options.MaxTotalRuleCount)
        {
            var message = $"Filter has {metrics.TotalRuleCount} total rules, but maximum allowed is {_options.MaxTotalRuleCount}";
            _logger.LogWarning("Guardrail violation: {Message}", message);
            return GuardrailValidationResult.Error(message);
        }

        // Soft limit: Complexity warning threshold
        if (metrics.TotalRuleCount > _options.ComplexityWarningThreshold)
        {
            var message = $"Filter complexity ({metrics.TotalRuleCount} rules, depth {metrics.MaxDepth}) may cause performance issues. " +
                         $"Consider simplifying or using server-side compatible filters.";
            _logger.LogInformation("Complexity warning: {Message}", message);
            return GuardrailValidationResult.Warning(message);
        }

        _logger.LogDebug("Filter passed guardrail validation: {Metrics}", metrics);
        return GuardrailValidationResult.Ok();
    }
}
