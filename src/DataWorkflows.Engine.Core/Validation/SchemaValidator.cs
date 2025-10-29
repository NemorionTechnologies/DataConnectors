using System.Text.Json;
using NJsonSchema;
using NJsonSchema.Validation;

namespace DataWorkflows.Engine.Core.Validation;

/// <summary>
/// NJsonSchema-based implementation of ISchemaValidator.
/// Validates data against JSON Schema draft 2020-12.
/// </summary>
public sealed class SchemaValidator : ISchemaValidator
{
    public SchemaValidationResult Validate(string schemaJson, string dataJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
            return SchemaValidationResult.Failure("Schema JSON cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(dataJson))
            return SchemaValidationResult.Failure("Data JSON cannot be null or empty.");

        try
        {
            // Parse the schema
            var schema = JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult();

            // Validate the data against the schema
            var errors = schema.Validate(dataJson);

            if (errors.Count == 0)
            {
                return SchemaValidationResult.Success();
            }

            // Convert validation errors to strings
            var errorMessages = errors.Select(FormatValidationError).ToList();
            return SchemaValidationResult.Failure(errorMessages);
        }
        catch (Exception ex)
        {
            return SchemaValidationResult.Failure($"Schema validation error: {ex.Message}");
        }
    }

    public SchemaValidationResult ValidateParameters(string schemaJson, Dictionary<string, object?> parameters)
    {
        if (parameters == null)
            return SchemaValidationResult.Failure("Parameters dictionary cannot be null.");

        try
        {
            // Convert parameters to JSON
            var dataJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            return Validate(schemaJson, dataJson);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.Failure($"Failed to serialize parameters to JSON: {ex.Message}");
        }
    }

    private static string FormatValidationError(ValidationError error)
    {
        var path = string.IsNullOrEmpty(error.Path) ? "root" : error.Path;
        var kind = error.Kind.ToString();

        if (!string.IsNullOrEmpty(error.Property))
        {
            return $"{path}.{error.Property}: {kind} - {error}";
        }

        return $"{path}: {kind} - {error}";
    }
}
