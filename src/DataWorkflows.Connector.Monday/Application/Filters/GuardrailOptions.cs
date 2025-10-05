namespace DataWorkflows.Connector.Monday.Application.Filters;

/// <summary>
/// Configuration options for Monday filter complexity guardrails.
/// </summary>
public sealed class GuardrailOptions
{
    public const string SectionName = "Guardrails";

    /// <summary>
    /// Enables or disables guardrail enforcement. Default: true.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Maximum allowed filter depth (nesting level). Default: 3.
    /// </summary>
    public int MaxDepth { get; set; } = 3;

    /// <summary>
    /// Maximum total rule count across all dimensions. Default: 50.
    /// </summary>
    public int MaxTotalRuleCount { get; set; } = 50;

    /// <summary>
    /// Complexity score threshold for soft warnings (logged but not blocked). Default: 30.
    /// </summary>
    public int ComplexityWarningThreshold { get; set; } = 30;
}
