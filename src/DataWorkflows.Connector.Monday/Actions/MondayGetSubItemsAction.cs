using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Queries.GetSubItems;
using DataWorkflows.Contracts.Actions;
using MediatR;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Actions;

/// <summary>
/// Workflow action for retrieving sub-items from a Monday.com parent item.
/// Implements the "monday.get-subitems" action type.
/// </summary>
public sealed class MondayGetSubItemsAction : IWorkflowAction
{
    private readonly IMediator _mediator;
    private readonly ILogger<MondayGetSubItemsAction> _logger;

    public string Type => "monday.get-subitems";

    public MondayGetSubItemsAction(
        IMediator mediator,
        ILogger<MondayGetSubItemsAction> logger)
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
                "Executing monday.get-subitems action for workflow {WorkflowExecutionId}, node {NodeId}",
                context.WorkflowExecutionId,
                context.NodeId);

            var parameters = DeserializeParameters(context.Parameters);

            var query = new GetSubItemsQuery(
                ParentItemId: parameters.ParentItemId,
                Filter: parameters.Filter);

            var items = await _mediator.Send(query, ct);

            var output = MapToOutput(items, parameters.ParentItemId);

            _logger.LogInformation(
                "Successfully retrieved {Count} sub-items from parent {ParentItemId} for node {NodeId}",
                output.Count,
                parameters.ParentItemId,
                context.NodeId);

            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Succeeded,
                Outputs: new Dictionary<string, object?>
                {
                    ["items"] = output.Items,
                    ["count"] = output.Count,
                    ["parentItemId"] = output.ParentItemId
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
            _logger.LogError(ex, "Error executing monday.get-subitems for node {NodeId}", context.NodeId);

            var isRetriable = IsRetriableError(ex);

            return new ActionExecutionResult(
                Status: isRetriable ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: ex.Message);
        }
    }

    private GetSubItemsParameters DeserializeParameters(Dictionary<string, object?> parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var typedParameters = JsonSerializer.Deserialize<GetSubItemsParameters>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (typedParameters == null)
        {
            throw new JsonException("Failed to deserialize parameters to GetSubItemsParameters");
        }

        return typedParameters;
    }

    private GetSubItemsOutput MapToOutput(IEnumerable<MondayItemDto> items, string parentItemId)
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

        return new GetSubItemsOutput
        {
            Items = itemsList,
            Count = itemsList.Count,
            ParentItemId = parentItemId
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
