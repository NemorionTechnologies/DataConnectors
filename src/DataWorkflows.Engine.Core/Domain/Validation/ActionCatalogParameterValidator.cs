using System.Collections.Generic;
using System.Linq;
using DataWorkflows.Engine.Core.Services;
using DataWorkflows.Engine.Core.Validation;

namespace DataWorkflows.Engine.Core.Domain.Validation;

/// <summary>
/// Parameter validator that validates action parameters against JSON schemas
/// stored in the ActionCatalog registry.
/// </summary>
public class ActionCatalogParameterValidator : IParameterValidator
{
    private readonly IActionCatalogRegistry _actionCatalogRegistry;
    private readonly ISchemaValidator _schemaValidator;

    public ActionCatalogParameterValidator(
        IActionCatalogRegistry actionCatalogRegistry,
        ISchemaValidator schemaValidator)
    {
        _actionCatalogRegistry = actionCatalogRegistry;
        _schemaValidator = schemaValidator;
    }

    public ParameterValidationResult Validate(string actionType, Dictionary<string, object?> parameters)
    {
        // Look up action in catalog
        var catalogEntry = _actionCatalogRegistry.GetAction(actionType);

        // If action not found in catalog, skip validation (could be a local action without schema)
        if (catalogEntry == null)
        {
            return ParameterValidationResult.Success();
        }

        // If no parameter schema defined, skip validation
        if (string.IsNullOrWhiteSpace(catalogEntry.ParameterSchemaJson))
        {
            return ParameterValidationResult.Success();
        }

        // Validate parameters against schema
        var validationResult = _schemaValidator.ValidateParameters(catalogEntry.ParameterSchemaJson, parameters);

        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors);
            return ParameterValidationResult.Fail($"Parameter validation failed for action '{actionType}': {errors}");
        }

        return ParameterValidationResult.Success();
    }
}
