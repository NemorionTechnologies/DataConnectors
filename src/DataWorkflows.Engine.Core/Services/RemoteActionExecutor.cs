using System.Net.Http.Json;
using System.Text.Json;
using DataWorkflows.Contracts.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Engine.Core.Services;

/// <summary>
/// Executes workflow actions on remote connectors via HTTP.
/// Looks up connector URLs from configuration and POST to their /api/v1/actions/execute endpoint.
/// </summary>
public sealed class RemoteActionExecutor : IRemoteActionExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RemoteActionExecutor> _logger;

    public RemoteActionExecutor(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RemoteActionExecutor> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActionExecutionResult> ExecuteRemoteActionAsync(
        string connectorId,
        string actionType,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Lookup connector URL from configuration
            var connectorUrl = GetConnectorUrl(connectorId);
            if (string.IsNullOrWhiteSpace(connectorUrl))
            {
                _logger.LogError("Connector URL not found for connector '{ConnectorId}'", connectorId);
                return new ActionExecutionResult(
                    Status: ActionExecutionStatus.Failed,
                    Outputs: new Dictionary<string, object?>(),
                    ErrorMessage: $"Connector URL not configured for '{connectorId}'. Check appsettings.json Connectors section.");
            }

            // Build request payload
            var request = new RemoteActionRequest
            {
                ActionType = actionType,
                Parameters = context.Parameters,
                ExecutionContext = new RemoteExecutionContext
                {
                    WorkflowExecutionId = context.WorkflowExecutionId,
                    NodeId = context.NodeId,
                    CorrelationId = Guid.NewGuid().ToString() // Generate correlation ID for tracing
                }
            };

            var executeUrl = $"{connectorUrl.TrimEnd('/')}/api/v1/actions/execute";
            _logger.LogInformation(
                "Executing remote action {ActionType} on connector {ConnectorId} at {Url} for workflow {WorkflowExecutionId}, node {NodeId}",
                actionType,
                connectorId,
                executeUrl,
                context.WorkflowExecutionId,
                context.NodeId);

            // Create HTTP client and send request
            var httpClient = _httpClientFactory.CreateClient("RemoteActions");
            var response = await httpClient.PostAsJsonAsync(executeUrl, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Remote action execution failed with HTTP {StatusCode} for {ActionType}: {ErrorContent}",
                    response.StatusCode,
                    actionType,
                    errorContent);

                return new ActionExecutionResult(
                    Status: ActionExecutionStatus.Failed,
                    Outputs: new Dictionary<string, object?>(),
                    ErrorMessage: $"Connector returned HTTP {response.StatusCode}: {errorContent}");
            }

            // Parse response
            var result = await response.Content.ReadFromJsonAsync<RemoteActionResponse>(cancellationToken);
            if (result == null)
            {
                _logger.LogError("Failed to deserialize response from connector {ConnectorId}", connectorId);
                return new ActionExecutionResult(
                    Status: ActionExecutionStatus.Failed,
                    Outputs: new Dictionary<string, object?>(),
                    ErrorMessage: "Failed to deserialize response from connector");
            }

            // Map response to ActionExecutionResult
            var status = ParseStatus(result.Status);
            _logger.LogInformation(
                "Remote action {ActionType} completed with status {Status} for node {NodeId}",
                actionType,
                status,
                context.NodeId);

            return new ActionExecutionResult(
                Status: status,
                Outputs: result.Outputs ?? new Dictionary<string, object?>(),
                ErrorMessage: result.Error);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error executing remote action {ActionType} on connector {ConnectorId}", actionType, connectorId);
            return new ActionExecutionResult(
                Status: ActionExecutionStatus.RetriableFailure,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: $"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout executing remote action {ActionType} on connector {ConnectorId}", actionType, connectorId);
            return new ActionExecutionResult(
                Status: ActionExecutionStatus.RetriableFailure,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: "Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing remote action {ActionType} on connector {ConnectorId}", actionType, connectorId);
            return new ActionExecutionResult(
                Status: ActionExecutionStatus.Failed,
                Outputs: new Dictionary<string, object?>(),
                ErrorMessage: $"Unexpected error: {ex.Message}");
        }
    }

    private string? GetConnectorUrl(string connectorId)
    {
        // Try environment variable first (for Docker/k8s deployments)
        var envVarName = $"CONNECTOR_{connectorId.ToUpperInvariant()}_URL";
        var envUrl = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            return envUrl;
        }

        // Fall back to configuration
        return _configuration[$"Connectors:{connectorId}:Url"];
    }

    private static ActionExecutionStatus ParseStatus(string status)
    {
        return status?.ToLowerInvariant() switch
        {
            "succeeded" => ActionExecutionStatus.Succeeded,
            "failed" => ActionExecutionStatus.Failed,
            "retriablefailure" => ActionExecutionStatus.RetriableFailure,
            "skipped" => ActionExecutionStatus.Skipped,
            _ => ActionExecutionStatus.Failed
        };
    }
}

/// <summary>
/// Request payload sent to remote connector's /api/v1/actions/execute endpoint.
/// </summary>
internal sealed record RemoteActionRequest
{
    public required string ActionType { get; init; }
    public required Dictionary<string, object?> Parameters { get; init; }
    public required RemoteExecutionContext ExecutionContext { get; init; }
}

/// <summary>
/// Execution context sent to remote connector.
/// </summary>
internal sealed record RemoteExecutionContext
{
    public Guid WorkflowExecutionId { get; init; }
    public required string NodeId { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Response from remote connector's /api/v1/actions/execute endpoint.
/// </summary>
internal sealed record RemoteActionResponse
{
    public required string Status { get; init; }
    public Dictionary<string, object?>? Outputs { get; init; }
    public string? Error { get; init; }
}
