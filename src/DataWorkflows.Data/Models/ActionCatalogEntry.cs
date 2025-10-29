namespace DataWorkflows.Data.Models;

/// <summary>
/// Represents an action registered in the ActionCatalog.
/// Actions are registered by connectors on startup and define what operations workflows can perform.
/// </summary>
public sealed record ActionCatalogEntry
{
    /// <summary>
    /// Unique identifier for this catalog entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Unique action type identifier (e.g., "monday.get-items", "core.echo").
    /// Format: {connectorId}.{action-name}
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// Identifier of the connector that provides this action (e.g., "monday", "core").
    /// </summary>
    public required string ConnectorId { get; init; }

    /// <summary>
    /// Human-readable display name for the action.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of what this action does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON Schema (draft 2020-12) describing the parameters this action expects.
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public required string ParameterSchemaJson { get; init; }

    /// <summary>
    /// JSON Schema (draft 2020-12) describing the outputs this action produces.
    /// These outputs are written to context.data[nodeId] for use by subsequent nodes.
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public required string OutputSchemaJson { get; init; }

    /// <summary>
    /// Indicates whether this action is currently enabled and can be used in workflows.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Indicates whether this action requires authentication/authorization.
    /// </summary>
    public bool RequiresAuth { get; init; } = true;

    /// <summary>
    /// Timestamp when this action was first registered.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when this action was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
