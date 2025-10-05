namespace DataWorkflows.Connector.Monday.Domain.Exceptions;

/// <summary>
/// Exception thrown when a filter definition violates configured complexity guardrails.
/// </summary>
public class GuardrailViolationException : Exception
{
    public string ViolationType { get; }
    public int ActualValue { get; }
    public int MaxAllowedValue { get; }

    public GuardrailViolationException(string violationType, int actualValue, int maxAllowedValue)
        : base($"Filter complexity violation: {violationType} is {actualValue}, but maximum allowed is {maxAllowedValue}.")
    {
        ViolationType = violationType;
        ActualValue = actualValue;
        MaxAllowedValue = maxAllowedValue;
    }

    public GuardrailViolationException(string message) : base(message)
    {
        ViolationType = "Unknown";
        ActualValue = 0;
        MaxAllowedValue = 0;
    }
}
