using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for creating a new sub-item under a parent item in Monday.com.
/// Implements the "monday.create-subitem" action type.
/// </summary>
public sealed class MondayCreateSubItemAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayCreateSubItemAction> _logger;

    public string Type => "monday.create-subitem";

    public MondayCreateSubItemAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayCreateSubItemAction> logger)
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
                "Executing monday.create-subitem action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var createdSubItem = await _mondayApiClient.CreateSubItemAsync(
                parameters.ParentItemId,
                parameters.ItemName,
                parameters.ColumnValues,
                ct);

            var output = MapToOutput(createdSubItem, parameters);

            _logger.LogInformation(
                "Successfully created sub-item {ItemId} under parent {ParentItemId} for node {NodeId}",
                createdSubItem.Id,
                parameters.ParentItemId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["item"] = output.Item,
                    ["parentItemId"] = output.ParentItemId,
                    ["itemId"] = output.ItemId,
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
            _logger.LogError(ex, "Error executing monday.create-subitem for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private CreateSubItemParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<CreateSubItemParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to CreateSubItemParameters");
        }

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(typedParameters.ParentItemId))
        {
            throw new ArgumentException("ParentItemId is required");
        }

        if (string.IsNullOrWhiteSpace(typedParameters.ItemName))
        {
            throw new ArgumentException("ItemName is required");
        }

        return typedParameters;
    }

    private CreateSubItemOutput MapToOutput(MondayItemDto item, CreateSubItemParameters parameters)
    {
        return new CreateSubItemOutput
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
            ParentItemId = parameters.ParentItemId,
            ItemId = item.Id,
            BoardId = item.GroupId  // Note: We're using GroupId temporarily; the GraphQL response should include board.id
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
