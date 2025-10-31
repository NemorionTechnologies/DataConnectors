using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for retrieving items with their sub-items and updates included.
/// Implements the "monday.get-items-with-details" action type.
/// This is a convenience action that saves making multiple separate API calls.
/// </summary>
public sealed class MondayGetItemsWithDetailsAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayGetItemsWithDetailsAction> _logger;

    public string Type => "monday.get-items-with-details";

    public MondayGetItemsWithDetailsAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayGetItemsWithDetailsAction> logger)
    {
        _mondayApiClient = mondayApiClient ?? throw new ArgumentNullException(nameof(mondayApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionContext context,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Executing monday.get-items-with-details action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var hydratedItems = await _mondayApiClient.GetHydratedBoardItemsAsync(
                parameters.BoardId,
                parameters.Filter,
                ct);

            var output = MapToOutput(hydratedItems, parameters.BoardId);

            _logger.LogInformation(
                "Successfully retrieved {Count} items with details from board {BoardId} for node {NodeId}",
                output.Count,
                parameters.BoardId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["items"] = output.Items,
                    ["count"] = output.Count,
                    ["boardId"] = output.BoardId
                },
                ErrorMessage: null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize parameters for node {NodeId}", context.NodeId);
            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: $"Invalid parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing monday.get-items-with-details for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private GetItemsWithDetailsParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<GetItemsWithDetailsParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to GetItemsWithDetailsParameters");
        }

        return typedParameters;
    }

    private GetItemsWithDetailsOutput MapToOutput(IEnumerable<Application.DTOs.MondayHydratedItemDto> hydratedItems, string boardId)
    {
        var itemsList = hydratedItems.Select(item => new MondayItemWithDetails
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Title = item.Title,
            GroupId = item.GroupId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            ColumnValues = item.ColumnValues.ToDictionary(
                kvp => kvp.Key,
                kvp => new ColumnValue
                {
                    Value = kvp.Value.Value,
                    Text = kvp.Value.Text
                }),
            SubItems = item.SubItems.Select(subItem => new MondayItem
            {
                Id = subItem.Id,
                ParentId = subItem.ParentId,
                Title = subItem.Title,
                GroupId = subItem.GroupId,
                CreatedAt = subItem.CreatedAt,
                UpdatedAt = subItem.UpdatedAt,
                ColumnValues = subItem.ColumnValues.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ColumnValue
                    {
                        Value = kvp.Value.Value,
                        Text = kvp.Value.Text
                    })
            }).ToList(),
            Updates = item.Updates.Select(update => new MondayUpdate
            {
                Id = update.Id,
                ItemId = update.ItemId,
                BodyText = update.BodyText,
                CreatorId = update.CreatorId,
                CreatedAt = update.CreatedAt
            }).ToList()
        }).ToList();

        return new GetItemsWithDetailsOutput
        {
            Items = itemsList,
            Count = itemsList.Count,
            BoardId = boardId
        };
    }

    private bool IsRetriableError(Exception ex)
    {
        var errorMessage = ex.Message.ToLowerInvariant();

        return errorMessage.Contains("timeout") ||
               errorMessage.Contains("rate limit") ||
               errorMessage.Contains("429") ||
               errorMessage.Contains("503") ||
               errorMessage.Contains("network") ||
               ex is HttpRequestException ||
               ex is TaskCanceledException;
    }
}
