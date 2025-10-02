namespace DataWorkflows.Connector.Monday.Application.DTOs;

/// <summary>
/// Represents an item or subitem with both parsed and raw column data.
/// This allows the workflow engine to consume parsed data without Monday-specific knowledge,
/// while still providing access to raw data for edge cases.
/// </summary>
public class ItemWithMetadata
{
    /// <summary>
    /// Unique identifier for the item
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Item title/name
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Type of item (Item or SubItem)
    /// </summary>
    public ItemType Type { get; set; }

    /// <summary>
    /// Parent item ID (only populated for SubItems)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Status column value (common across most boards)
    /// </summary>
    public string Status { get; set; } = "No Status";

    /// <summary>
    /// Timeline column value (common across most boards)
    /// </summary>
    public TimelineValue? Timeline { get; set; }

    /// <summary>
    /// Last update date (when item had activity)
    /// </summary>
    public DateTime? LastUpdateDate { get; set; }

    /// <summary>
    /// Parsed column values as native .NET types (string, DateTime, int, etc.)
    /// for easy consumption by workflow engines without Monday-specific knowledge.
    /// Key is the column title (human-readable).
    /// </summary>
    public Dictionary<string, object?> ParsedColumns { get; set; } = new();

    /// <summary>
    /// Raw Monday.com column values with both JSON and text representations.
    /// Use this as a fallback when parsed values aren't sufficient.
    /// Key is the column ID (Monday-specific).
    /// </summary>
    public Dictionary<string, MondayColumnValueDto> RawColumns { get; set; } = new();
}

public enum ItemType
{
    Item,
    SubItem
}
