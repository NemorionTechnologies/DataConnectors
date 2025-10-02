namespace DataWorkflows.Connector.Monday.Application.Interfaces;

/// <summary>
/// Service to resolve column titles to column IDs using cached metadata.
/// </summary>
public interface IColumnResolverService
{
    /// <summary>
    /// Resolves a column identifier (either ID or title) to a column ID.
    /// If columnId is provided, returns it directly.
    /// If columnTitle is provided, resolves it to a column ID (case-insensitive).
    /// </summary>
    /// <param name="boardId">The board ID</param>
    /// <param name="columnId">Optional column ID (takes precedence)</param>
    /// <param name="columnTitle">Optional column title</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resolved column ID</returns>
    /// <exception cref="ArgumentException">When neither columnId nor columnTitle is provided</exception>
    /// <exception cref="Domain.Exceptions.ResourceNotFoundException">When column title is not found</exception>
    Task<string> ResolveColumnIdAsync(
        string boardId,
        string? columnId,
        string? columnTitle,
        CancellationToken cancellationToken);
}
