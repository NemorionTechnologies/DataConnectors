using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for retrieving updates from a Monday.com item.
/// Implements the "monday.get-item-updates" action type.
/// </summary>
public sealed class MondayGetItemUpdatesAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayGetItemUpdatesAction> _logger;

    public string Type => "monday.get-item-updates";

    public MondayGetItemUpdatesAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayGetItemUpdatesAction> logger)
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
                "Executing monday.get-item-updates action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var updates = await _mondayApiClient.GetItemUpdatesAsync(
                parameters.ItemId,
                parameters.FromDate,
                parameters.ToDate,
                ct);

            var output = MapToOutput(updates, parameters.ItemId);

            _logger.LogInformation(
                "Successfully retrieved {Count} updates from item {ItemId} for node {NodeId}",
                output.Count,
                parameters.ItemId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["updates"] = output.Updates,
                    ["count"] = output.Count,
                    ["itemId"] = output.ItemId
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
            _logger.LogError(ex, "Error executing monday.get-item-updates for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private GetItemUpdatesParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<GetItemUpdatesParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to GetItemUpdatesParameters");
        }

        return typedParameters;
    }

    private GetItemUpdatesOutput MapToOutput(IEnumerable<MondayUpdateDto> updates, string itemId)
    {
        var updatesList = updates.Select(update => new MondayUpdate
        {
            Id = update.Id,
            ItemId = update.ItemId,
            BodyText = update.BodyText,
            CreatorId = update.CreatorId,
            CreatedAt = update.CreatedAt
        }).ToList();

        return new GetItemUpdatesOutput
        {
            Updates = updatesList,
            Count = updatesList.Count,
            ItemId = itemId
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
