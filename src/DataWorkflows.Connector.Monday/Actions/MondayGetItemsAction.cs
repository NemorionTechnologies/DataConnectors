using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for retrieving items from a Monday.com board.
/// Implements the "monday.get-items" action type.
/// </summary>
public sealed class MondayGetItemsAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayGetItemsAction> _logger;

    public string Type => "monday.get-items";

    public MondayGetItemsAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayGetItemsAction> logger)
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
                "Executing monday.get-items action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var items = await _mondayApiClient.GetBoardItemsAsync(
                parameters.BoardId,
                parameters.Filter,
                ct);

            var output = MapToOutput(items, parameters.BoardId);

            _logger.LogInformation(
                "Successfully retrieved {Count} items from board {BoardId} for node {NodeId}",
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
            _logger.LogError(ex, "Error executing monday.get-items for node {NodeId}", context.NodeId);

            // Determine if this is retriable
            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private GetItemsParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        // Serialize dictionary back to JSON and deserialize to typed parameters
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<GetItemsParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to GetItemsParameters");
        }

        return typedParameters;
    }

    private GetItemsOutput MapToOutput(IEnumerable<MondayItemDto> items, string boardId)
    {
        var itemsList = items.Select(item => new MondayItem
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
                })
        }).ToList();

        return new GetItemsOutput
        {
            Items = itemsList,
            Count = itemsList.Count,
            BoardId = boardId
        };
    }

    private bool IsRetriableError(Exception ex)
    {
        // Determine if the error is transient and can be retried
        // Common retriable errors: network issues, rate limits, timeouts
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
