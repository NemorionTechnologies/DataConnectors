using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Contracts.Actions;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for retrieving all updates from a Monday.com board.
/// Implements the "monday.get-board-updates" action type.
/// </summary>
public sealed class MondayGetBoardUpdatesAction : IWorkflowAction
{
    private readonly IMondayApiClient _mondayApiClient;
    private readonly ILogger<MondayGetBoardUpdatesAction> _logger;

    public string Type => "monday.get-board-updates";

    public MondayGetBoardUpdatesAction(
        IMondayApiClient mondayApiClient,
        ILogger<MondayGetBoardUpdatesAction> logger)
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
                "Executing monday.get-board-updates action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var updates = await _mondayApiClient.GetBoardUpdatesAsync(
                parameters.BoardId,
                parameters.FromDate,
                parameters.ToDate,
                ct);

            var output = MapToOutput(updates, parameters.BoardId);

            _logger.LogInformation(
                "Successfully retrieved {Count} updates from board {BoardId} for node {NodeId}",
                output.Count,
                parameters.BoardId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["updates"] = output.Updates,
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
            _logger.LogError(ex, "Error executing monday.get-board-updates for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private GetBoardUpdatesParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<GetBoardUpdatesParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to GetBoardUpdatesParameters");
        }

        return typedParameters;
    }

    private GetBoardUpdatesOutput MapToOutput(IEnumerable<Application.DTOs.MondayUpdateDto> updates, string boardId)
    {
        var updatesList = updates.Select(update => new MondayUpdate
        {
            Id = update.Id,
            ItemId = update.ItemId,
            BodyText = update.BodyText,
            CreatorId = update.CreatorId,
            CreatedAt = update.CreatedAt
        }).ToList();

        return new GetBoardUpdatesOutput
        {
            Updates = updatesList,
            Count = updatesList.Count,
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
