namespace DataWorkflows.Contracts;

/// <summary>
/// Standard DTO for representing a generic item from any connector service.
/// </summary>
public class ConnectorItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
