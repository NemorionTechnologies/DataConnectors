namespace DataWorkflows.Connector.Monday.Application.DTOs;

public class MondayItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Column values with both raw JSON and human-readable text.
    /// Key is the column ID, value contains both the JSON value and display text.
    /// </summary>
    public Dictionary<string, MondayColumnValueDto> ColumnValues { get; set; } = new();
}
