using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for updating a column value on a Monday.com sub-item.
/// Implements the "monday.update-subitem-column" action type.
///
/// NOTE: This is a cosmetic alias for non-technical users. Technically, sub-items ARE items
/// in Monday.com, and the same UpdateColumnValueCommand handles both. This action exists
/// purely for semantic clarity and discoverability.
/// </summary>
public sealed class MondayUpdateSubItemColumnAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayUpdateSubItemColumnAction> _logger;

    public string Type => "monday.update-subitem-column";

    public MondayUpdateSubItemColumnAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayUpdateSubItemColumnAction> logger)
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
                "Executing monday.update-subitem-column action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            // Serialize the value object to JSON
            var valueJson = JsonSerializer.Serialize(parameters.Value);

            // Determine the column ID to use
            var columnId = parameters.ColumnId ?? parameters.ColumnTitle ?? throw new ArgumentException("Either ColumnId or ColumnTitle must be provided");

            // Use the same API call as regular item updates - sub-items ARE items!
            var updatedItem = await _mondayApiClient.UpdateColumnValueAsync(
                parameters.BoardId,
                parameters.ItemId,  // This ItemId is actually a sub-item ID
                columnId,
                valueJson,
                ct);

            var output = MapToOutput(updatedItem, parameters);

            _logger.LogInformation(
                "Successfully updated column on sub-item {ItemId} in board {BoardId} for node {NodeId}",
                parameters.ItemId,
                parameters.BoardId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["item"] = output.Item,
                    ["boardId"] = output.BoardId,
                    ["itemId"] = output.ItemId,
                    ["columnId"] = output.ColumnId
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
            _logger.LogError(ex, "Error executing monday.update-subitem-column for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private UpdateSubItemColumnParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<UpdateSubItemColumnParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to UpdateSubItemColumnParameters");
        }

        // Validate that either ColumnId or ColumnTitle is provided
        if (string.IsNullOrWhiteSpace(typedParameters.ColumnId) && string.IsNullOrWhiteSpace(typedParameters.ColumnTitle))
        {
            throw new ArgumentException("Either ColumnId or ColumnTitle must be provided");
        }

        return typedParameters;
    }

    private UpdateSubItemColumnOutput MapToOutput(MondayItemDto item, UpdateSubItemColumnParameters parameters)
    {
        return new UpdateSubItemColumnOutput
        {
            Item = new MondayItem
            {
                Id = item.Id,
                ParentId = item.ParentId,  // This will be populated for sub-items
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
            ItemId = parameters.ItemId,
            ColumnId = parameters.ColumnId ?? parameters.ColumnTitle ?? string.Empty
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
