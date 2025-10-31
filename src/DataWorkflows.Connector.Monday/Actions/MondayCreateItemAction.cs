using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for creating a new item on a Monday.com board.
/// Implements the "monday.create-item" action type.
/// </summary>
public sealed class MondayCreateItemAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayCreateItemAction> _logger;

    public string Type => "monday.create-item";

    public MondayCreateItemAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayCreateItemAction> logger)
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
                "Executing monday.create-item action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var createdItem = await _mondayApiClient.CreateItemAsync(
                parameters.BoardId,
                parameters.ItemName,
                parameters.GroupId,
                parameters.ColumnValues,
                ct);

            var output = MapToOutput(createdItem, parameters);

            _logger.LogInformation(
                "Successfully created item {ItemId} on board {BoardId} for node {NodeId}",
                createdItem.Id,
                parameters.BoardId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["item"] = output.Item,
                    ["boardId"] = output.BoardId,
                    ["itemId"] = output.ItemId,
                    ["groupId"] = output.GroupId
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
            _logger.LogError(ex, "Error executing monday.create-item for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private CreateItemParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<CreateItemParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to CreateItemParameters");
        }

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(typedParameters.BoardId))
        {
            throw new ArgumentException("BoardId is required");
        }

        if (string.IsNullOrWhiteSpace(typedParameters.ItemName))
        {
            throw new ArgumentException("ItemName is required");
        }

        return typedParameters;
    }

    private CreateItemOutput MapToOutput(MondayItemDto item, CreateItemParameters parameters)
    {
        return new CreateItemOutput
        {
            Item = new MondayItem
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
            },
            BoardId = parameters.BoardId,
            ItemId = item.Id,
            GroupId = item.GroupId
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
