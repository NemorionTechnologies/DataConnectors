using NJsonSchema;
using NJsonSchema.Generation;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions.Schemas;

/// <summary>
/// Helper class for generating JSON Schemas from C# types.
/// Used to create ParameterSchema and OutputSchema for action registration.
/// </summary>
public static class SchemaGenerator
{
    /// <summary>
    /// Generates a JSON Schema (draft 2020-12) for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to generate a schema for.</typeparam>
    /// <returns>JSON Schema as a string.</returns>
    public static string GenerateSchema<T>()
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            // Use JSON Schema draft 2020-12 as specified in requirements
            SchemaType = SchemaType.JsonSchema,

            // Generate schemas that are strict but user-friendly
            AlwaysAllowAdditionalObjectProperties = false,
            GenerateAbstractProperties = false,
            FlattenInheritanceHierarchy = true,

            // Use camelCase naming
            SerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
        };

        var generator = new JsonSchemaGenerator(settings);
        var schema = generator.Generate(typeof(T));

        return schema.ToJson();
    }
}
