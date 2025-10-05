namespace DataWorkflows.Connector.Monday.Application.Filters;

/// <summary>
/// Result of validating a filter definition against guardrails.
/// </summary>
public sealed record GuardrailValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }

    public static GuardrailValidationResult Ok() =>
        new() { IsValid = true };

    public static GuardrailValidationResult Error(string message) =>
        new() { IsValid = false, ErrorMessage = message };

    public static GuardrailValidationResult Warning(string message) =>
        new() { IsValid = true, WarningMessage = message };
}
