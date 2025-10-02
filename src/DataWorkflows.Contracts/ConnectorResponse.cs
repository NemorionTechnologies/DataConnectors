namespace DataWorkflows.Contracts;

/// <summary>
/// Standard response wrapper for connector operations.
/// </summary>
public class ConnectorResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
