using System.Collections.Generic;

namespace DataWorkflows.Engine.Validation;

public sealed class NoopParameterValidator : IParameterValidator
{
    public ParameterValidationResult Validate(string actionType, Dictionary<string, object?> parameters)
    {
        // Placeholder for future JSON Schema validation per action.
        return ParameterValidationResult.Success();
    }
}

