using System.Collections.Generic;

namespace DataWorkflows.Engine.Core.Domain.Validation;

public interface IParameterValidator
{
    ParameterValidationResult Validate(string actionType, Dictionary<string, object?> parameters);
}

public sealed record ParameterValidationResult(bool IsValid, string? ErrorMessage = null)
{
    public static ParameterValidationResult Success() => new(true, null);
    public static ParameterValidationResult Fail(string message) => new(false, message);
}

