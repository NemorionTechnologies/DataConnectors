using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Queries.GetBoardActivity;
using DataWorkflows.Contracts.Actions;
using MediatR;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for retrieving activity log from a Monday.com board.
/// Implements the "monday.get-board-activity" action type.
/// </summary>
public sealed class MondayGetBoardActivityAction : IWorkflowAction
{
    private readonly IMediator _mediator;
    private readonly ILogger<MondayGetBoardActivityAction> _logger;

    public string Type => "monday.get-board-activity";

    public MondayGetBoardActivityAction(
        IMediator mediator,
        ILogger<MondayGetBoardActivityAction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionContext context,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Executing monday.get-board-activity action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var query = new GetBoardActivityQuery(
                BoardId: parameters.BoardId,
                FromDate: parameters.FromDate,
                ToDate: parameters.ToDate);

            var activities = await _mediator.Send(query, ct);

            var output = MapToOutput(activities, parameters.BoardId);

            _logger.LogInformation(
                "Successfully retrieved {Count} activity logs from board {BoardId} for node {NodeId}",
                output.Count,
                parameters.BoardId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["activities"] = output.Activities,
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
            _logger.LogError(ex, "Error executing monday.get-board-activity for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private GetBoardActivityParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<GetBoardActivityParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to GetBoardActivityParameters");
        }

        return typedParameters;
    }

    private GetBoardActivityOutput MapToOutput(IEnumerable<MondayActivityLogDto> activities, string boardId)
    {
        var activitiesList = activities.Select(activity => new MondayActivityLog
        {
            EventType = activity.EventType,
            UserId = activity.UserId,
            ItemId = activity.ItemId,
            CreatedAt = activity.CreatedAt,
            EventDataJson = activity.EventDataJson
        }).ToList();

        return new GetBoardActivityOutput
        {
            Activities = activitiesList,
            Count = activitiesList.Count,
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
