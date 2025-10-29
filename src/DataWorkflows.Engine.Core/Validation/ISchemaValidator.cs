namespace DataWorkflows.Engine.Core.Validation;

/// <summary>
/// Service for validating data against JSON Schema (draft 2020-12).
/// Used to validate workflow action parameters against ActionCatalog schemas.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates a JSON payload against a JSON Schema.
    /// </summary>
    /// <param name="schemaJson">The JSON Schema to validate against.</param>
    /// <param name="dataJson">The JSON data to validate.</param>
    /// <returns>A validation result containing any errors found.</returns>
    SchemaValidationResult Validate(string schemaJson, string dataJson);

    /// <summary>
    /// Validates a dictionary of parameters against a JSON Schema.
    /// Converts the dictionary to JSON before validation.
    /// </summary>
    /// <param name="schemaJson">The JSON Schema to validate against.</param>
    /// <param name="parameters">The parameters dictionary to validate.</param>
    /// <returns>A validation result containing any errors found.</returns>
    SchemaValidationResult ValidateParameters(string schemaJson, Dictionary<string, object?> parameters);
}

/// <summary>
/// Result of a JSON Schema validation operation.
/// </summary>
public sealed record SchemaValidationResult
{
    /// <summary>
    /// Indicates whether the validation passed (no errors).
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Collection of validation errors, if any.
    /// Empty if IsValid is true.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static SchemaValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static SchemaValidationResult Failure(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static SchemaValidationResult Failure(string error) =>
        new() { IsValid = false, Errors = new[] { error } };
}
