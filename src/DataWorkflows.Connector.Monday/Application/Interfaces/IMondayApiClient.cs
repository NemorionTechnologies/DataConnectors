using DataWorkflows.Connector.Monday.Application.DTOs;

namespace DataWorkflows.Connector.Monday.Application.Interfaces;

public interface IMondayApiClient
{
    // Read Operations
    Task<IEnumerable<MondayItemDto>> GetBoardItemsAsync(
        string boardId,
        GetItemsFilterModel filter,
        CancellationToken cancellationToken);

    Task<IEnumerable<MondayActivityLogDto>> GetBoardActivityAsync(
        string boardId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken);

    Task<IEnumerable<MondayUpdateDto>> GetBoardUpdatesAsync(
        string boardId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken);

    Task<IEnumerable<MondayItemDto>> GetSubItemsAsync(
        string parentItemId,
        GetItemsFilterModel filter,
        CancellationToken cancellationToken);

    Task<IEnumerable<MondayUpdateDto>> GetItemUpdatesAsync(
        string itemId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken);

    Task<IEnumerable<MondayHydratedItemDto>> GetHydratedBoardItemsAsync(
        string boardId,
        GetItemsFilterModel filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ColumnMetadata>> GetBoardColumnsAsync(
        string boardId,
        CancellationToken cancellationToken);

    // Write Operations
    Task<MondayItemDto> UpdateColumnValueAsync(
        string boardId,
        string itemId,
        string columnId,
        string valueJson,
        CancellationToken cancellationToken);
}
