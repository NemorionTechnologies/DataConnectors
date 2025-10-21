using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataWorkflows.Contracts.Actions;
using DataWorkflows.Data.Repositories;
using DataWorkflows.Engine.Configuration;
using DataWorkflows.Engine.Evaluation;
using DataWorkflows.Engine.Execution;
using DataWorkflows.Engine.Models;
using DataWorkflows.Engine.Registry;
using DataWorkflows.Engine.Validation;
using DataWorkflows.Engine.Templating;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Engine.Orchestration;

public class WorkflowConductor
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyReadOnlyDictionary =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    private readonly ActionRegistry _registry;
    private readonly OrchestrationOptions _options;
    private readonly ITemplateEngine _templateEngine;
    private readonly IParameterValidator _parameterValidator;
    private readonly ILogger<WorkflowConductor> _logger;

    public WorkflowConductor(ActionRegistry registry, OrchestrationOptions options, ITemplateEngine templateEngine, IParameterValidator parameterValidator, ILogger<WorkflowConductor> logger)
    {
        _registry = registry;
        _options = options;
        _templateEngine = templateEngine;
        _parameterValidator = parameterValidator;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object> trigger,
        Dictionary<string, object>? vars,
        string requestId,
        string connectionString)
    {
        ValidateWorkflowOrThrow(workflow);

        var executionRepo = new WorkflowExecutionRepository(connectionString);
        var actionRepo = new ActionExecutionRepository(connectionString);
        var context = new WorkflowContext();
        using var workflowCts = new CancellationTokenSource(_options.DefaultWorkflowTimeout);

        var triggerModel = CreateReadOnlyDictionary(trigger);
        var varsModel = vars is null ? EmptyReadOnlyDictionary : new ReadOnlyDictionary<string, object?>(vars.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));

        Guid executionId = Guid.Empty;
        var workflowStatus = "Failed";
        var executionCompleted = false;

        try
        {
            var executionStart = DateTime.UtcNow;
            executionId = await executionRepo.CreateExecution(
                workflowId: workflow.Id,
                version: 1,
                requestId: requestId,
                triggerJson: JsonSerializer.Serialize(trigger));

            await executionRepo.MarkExecutionRunning(executionId, executionStart);

            var outcome = await RunWorkflowAsync(
                executionId,
                workflow,
                triggerModel,
                varsModel,
                context,
                actionRepo,
                workflowCts);

            workflowStatus = outcome.Status;
        }
        catch (WorkflowValidationException)
        {
            throw;
        }
        catch
        {
            if (executionId != Guid.Empty)
            {
                var failedSnapshot = JsonSerializer.Serialize(context.GetAllOutputs());
                await executionRepo.CompleteExecution(executionId, "Failed", DateTime.UtcNow, failedSnapshot);
                executionCompleted = true;
            }

            throw;
        }

        if (executionId != Guid.Empty && !executionCompleted)
        {
            var completedAt = DateTime.UtcNow;
            var snapshotJson = JsonSerializer.Serialize(context.GetAllOutputs());
            await executionRepo.CompleteExecution(executionId, workflowStatus, completedAt, snapshotJson);
            return new ExecutionResult(executionId, workflowStatus, completedAt);
        }

        return new ExecutionResult(executionId, workflowStatus, DateTime.UtcNow);
    }

    private void ValidateWorkflowOrThrow(WorkflowDefinition workflow)
    {
        try
        {
            var validator = new GraphValidator();
            validator.Validate(workflow);
        }
        catch (Exception ex)
        {
            throw new WorkflowValidationException(workflow.Id, ex);
        }
    }

    private async Task<WorkflowRunOutcome> RunWorkflowAsync(
        Guid executionId,
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, object?> triggerModel,
        IReadOnlyDictionary<string, object?> varsModel,
        WorkflowContext context,
        ActionExecutionRepository actionRepo,
        CancellationTokenSource workflowCts)
    {
        var nodesById = workflow.Nodes.ToDictionary(node => node.Id);
        var incomingEdgeStates = BuildIncomingEdgeState(workflow);
        var runQueue = new ConcurrentQueue<string>();
        var enqueuedNodes = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var completedNodes = new ConcurrentDictionary<string, ActionExecutionStatus>(StringComparer.OrdinalIgnoreCase);
        var runningTasks = new List<Task>();
        using var semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxParallelActions));
        var evaluator = new JintConditionEvaluator();

        runQueue.Enqueue(workflow.StartNode);
        enqueuedNodes.TryAdd(workflow.StartNode, true);

        var workflowFailed = false;
        var workflowStatus = "Succeeded";
        var token = workflowCts.Token;

        void FailWorkflow()
        {
            workflowFailed = true;
            workflowStatus = "Failed";
            workflowCts.Cancel();
        }

        while (!token.IsCancellationRequested)
        {
            while (runQueue.TryDequeue(out var nodeId))
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (completedNodes.ContainsKey(nodeId))
                {
                    continue;
                }

                if (!nodesById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                var task = RunNodeAsync(
                    executionId,
                    node,
                    context,
                    actionRepo,
                evaluator,
                triggerModel,
                varsModel,
                runQueue,
                    enqueuedNodes,
                    completedNodes,
                    incomingEdgeStates,
                    workflowCts,
                    semaphore,
                    FailWorkflow);

                runningTasks.Add(task);
            }

            if (runningTasks.Count == 0)
            {
                if (runQueue.IsEmpty)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), token);
                continue;
            }

            var completedTask = await Task.WhenAny(runningTasks);
            runningTasks.Remove(completedTask);

            try
            {
                await completedTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation has been requested.
            }
            catch
            {
                FailWorkflow();
            }
        }

        if (runningTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(runningTasks);
            }
            catch (OperationCanceledException)
            {
                // Suppress cancellation exceptions during shutdown.
            }
        }

        if (token.IsCancellationRequested && !workflowFailed)
        {
            workflowStatus = "Cancelled";
        }

        return new WorkflowRunOutcome(workflowStatus);
    }

    private async Task RunNodeAsync(
        Guid executionId,
        Node node,
        WorkflowContext context,
        ActionExecutionRepository actionRepo,
        JintConditionEvaluator evaluator,
        IReadOnlyDictionary<string, object?> triggerModel,
        IReadOnlyDictionary<string, object?> varsModel,
        ConcurrentQueue<string> runQueue,
        ConcurrentDictionary<string, bool> enqueuedNodes,
        ConcurrentDictionary<string, ActionExecutionStatus> completedNodes,
        Dictionary<string, ConcurrentDictionary<string, EdgeOutcome>> incomingEdgeStates,
        CancellationTokenSource workflowCts,
        SemaphoreSlim semaphore,
        Action failWorkflow)
    {
        if (workflowCts.IsCancellationRequested)
        {
            return;
        }

        var status = await ExecuteNodeWithRetries(
            executionId,
            node,
            context,
            actionRepo,
            triggerModel,
            varsModel,
            semaphore,
            workflowCts);

        completedNodes[node.Id] = status;

        if (status != ActionExecutionStatus.Succeeded)
        {
            if (status == ActionExecutionStatus.Failed && !workflowCts.IsCancellationRequested)
            {
                failWorkflow();
            }

            return;
        }

        if (node.Edges is null || node.Edges.Count == 0)
        {
            return;
        }

        var contextSnapshot = CreateReadOnlyDictionaryFromOutputs(context.GetAllOutputs());
        var scope = new ConditionScope(triggerModel, contextSnapshot, varsModel);
        var allowMultiple = !string.Equals(node.RoutePolicy, "firstMatch", StringComparison.OrdinalIgnoreCase);

        for (var index = 0; index < node.Edges.Count; index++)
        {
            var edge = node.Edges[index];
            var satisfied = EvaluateEdge(edge, status, scope, evaluator);
            UpdateEdgeState(node.Id, edge.TargetNode, satisfied, incomingEdgeStates);
            TryEnqueueNode(edge.TargetNode, runQueue, enqueuedNodes, incomingEdgeStates, workflowCts.Token);

            if (!allowMultiple && satisfied)
            {
                for (var remaining = index + 1; remaining < node.Edges.Count; remaining++)
                {
                    var laterEdge = node.Edges[remaining];
                    UpdateEdgeState(node.Id, laterEdge.TargetNode, false, incomingEdgeStates);
                    TryEnqueueNode(laterEdge.TargetNode, runQueue, enqueuedNodes, incomingEdgeStates, workflowCts.Token);
                }

                break;
            }
        }
    }

    private async Task<ActionExecutionStatus> ExecuteNodeWithRetries(
        Guid executionId,
        Node node,
        WorkflowContext context,
        ActionExecutionRepository actionRepo,
        IReadOnlyDictionary<string, object?> triggerModel,
        IReadOnlyDictionary<string, object?> varsModel,
        SemaphoreSlim semaphore,
        CancellationTokenSource workflowCts)
    {
        var action = _registry.GetAction(node.ActionType);
        var policies = node.Policies ?? new NodePolicies(false);

        var attemptNumber = 1;
        var maxAttempts = Math.Max(1, _options.RetryPolicy.MaxAttempts);

        while (!workflowCts.IsCancellationRequested)
        {
            ActionExecutionResult attemptResult;
            var attemptStart = DateTime.UtcNow;
            string? parametersJson = null;
            Dictionary<string, object?>? renderedParameters = null;

            try
            {
                await semaphore.WaitAsync(workflowCts.Token);
            }
            catch (OperationCanceledException)
            {
                return ActionExecutionStatus.Skipped;
            }

            try
            {
                using var actionCts = CancellationTokenSource.CreateLinkedTokenSource(workflowCts.Token);
                actionCts.CancelAfter(_options.DefaultActionTimeout);

                try
                {
                    // Render parameters per policies
                    var shouldRerender = policies.RerenderOnRetry;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    if (attemptNumber == 1 || shouldRerender)
                    {
                        parametersJson = await RenderParametersJsonAsync(node, triggerModel, context, varsModel, actionCts.Token);
                    }
                    else
                    {
                        // Reuse first attempt's persisted parameters
                        parametersJson = await actionRepo.GetFirstAttemptParameters(executionId, node.Id);
                        if (string.IsNullOrWhiteSpace(parametersJson))
                        {
                            // Fallback: render if not found (should not happen normally)
                            parametersJson = await RenderParametersJsonAsync(node, triggerModel, context, varsModel, actionCts.Token);
                        }
                    }
                    sw.Stop();
                    _logger.LogInformation("Rendered parameters {ExecutionId} {NodeId} attempt={Attempt} duration_ms={DurationMs}", executionId, node.Id, attemptNumber, sw.ElapsedMilliseconds);

                    renderedParameters = DeserializeParameters(parametersJson!);

                    // Validate rendered parameters
                    var validation = _parameterValidator.Validate(node.ActionType, renderedParameters);
                    if (!validation.IsValid)
                    {
                        throw new InvalidOperationException($"Parameter validation failed: {validation.ErrorMessage}");
                    }

                    var actionContext = new ActionExecutionContext(
                        WorkflowExecutionId: executionId,
                        NodeId: node.Id,
                        Parameters: renderedParameters,
                        Services: null!);

                    attemptResult = await action.ExecuteAsync(actionContext, actionCts.Token);
                }
                catch (OperationCanceledException) when (!workflowCts.IsCancellationRequested)
                {
                    attemptResult = new ActionExecutionResult(
                        ActionExecutionStatus.Failed,
                        new Dictionary<string, object?>(),
                        "Action timed out.");
                }
                catch (Exception ex)
                {
                    // Map template/validation errors to RetriableFailure if attempts remain; else Failed
                    var canRetry = attemptNumber < Math.Max(1, _options.RetryPolicy.MaxAttempts);
                    var status = canRetry ? ActionExecutionStatus.RetriableFailure : ActionExecutionStatus.Failed;
                    _logger.LogWarning(ex, "Template/validation error {ExecutionId} {NodeId} attempt={Attempt} template_error=true", executionId, node.Id, attemptNumber);
                    attemptResult = new ActionExecutionResult(
                        status,
                        new Dictionary<string, object?>(),
                        ex.Message);
                }
            }
            finally
            {
                semaphore.Release();
            }

            var attemptEnd = DateTime.UtcNow;

            var outputsJson = attemptResult.Status == ActionExecutionStatus.Succeeded
                ? JsonSerializer.Serialize(attemptResult.Outputs)
                : null;

            string? errorJson = attemptResult.Status == ActionExecutionStatus.Succeeded
                ? null
                : JsonSerializer.Serialize(new
                {
                    message = attemptResult.ErrorMessage ?? "Unspecified error",
                    status = attemptResult.Status.ToString()
                });

            await actionRepo.RecordExecution(
                executionId,
                node.Id,
                node.ActionType,
                attemptResult.Status.ToString(),
                attemptNumber,
                retryCount: Math.Max(0, attemptNumber - 1),
                parameters: parametersJson,
                outputsJson,
                errorJson,
                attemptStart,
                attemptEnd);

            if (attemptResult.Status == ActionExecutionStatus.Succeeded)
            {
                context.SetActionOutput(node.Id, attemptResult.Outputs);
                return ActionExecutionStatus.Succeeded;
            }

            var shouldRetry = attemptResult.Status == ActionExecutionStatus.RetriableFailure
                               && attemptNumber < maxAttempts
                               && !workflowCts.IsCancellationRequested;

            if (!shouldRetry)
            {
                return ActionExecutionStatus.Failed;
            }

            attemptNumber++;
            var delay = CalculateRetryDelay(attemptNumber);

            try
            {
                await Task.Delay(delay, workflowCts.Token);
            }
            catch (OperationCanceledException)
            {
                return ActionExecutionStatus.Failed;
            }
        }

        return ActionExecutionStatus.Skipped;
    }

    private async Task<string> RenderParametersJsonAsync(
        Node node,
        IReadOnlyDictionary<string, object?> triggerModel,
        WorkflowContext context,
        IReadOnlyDictionary<string, object?> varsModel,
        CancellationToken ct)
    {
        var parameters = NormalizeParameters(node.Parameters);
        var templateJson = JsonSerializer.Serialize(parameters);
        // Build model: { trigger, context: { data = ... }, vars }
        var contextSnapshot = CreateReadOnlyDictionaryFromOutputs(context.GetAllOutputs());
        var model = new
        {
            trigger = triggerModel,
            context = new { data = (object)contextSnapshot },
            vars = varsModel
        };

        return await _templateEngine.RenderAsync(templateJson, model, ct);
    }

    private static Dictionary<string, object?> DeserializeParameters(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Rendered parameters must be a JSON object");
        }

        var dict = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>();
                foreach (var p in el.EnumerateObject()) obj[p.Name] = JsonElementToObject(p.Value);
                return obj;
            case JsonValueKind.Array:
                return el.EnumerateArray().Select(JsonElementToObject).ToList();
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var l)) return l;
                if (el.TryGetDouble(out var d)) return d;
                return el.GetRawText();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static Dictionary<string, ConcurrentDictionary<string, EdgeOutcome>> BuildIncomingEdgeState(WorkflowDefinition workflow)
    {
        var incoming = new Dictionary<string, ConcurrentDictionary<string, EdgeOutcome>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in workflow.Nodes)
        {
            if (node.Edges is null)
            {
                continue;
            }

            foreach (var edge in node.Edges)
            {
                if (!incoming.TryGetValue(edge.TargetNode, out var states))
                {
                    states = new ConcurrentDictionary<string, EdgeOutcome>(StringComparer.OrdinalIgnoreCase);
                    incoming[edge.TargetNode] = states;
                }

                states.TryAdd(node.Id, EdgeOutcome.Unknown);
            }
        }

        return incoming;
    }

    private static void UpdateEdgeState(
        string parentId,
        string targetId,
        bool satisfied,
        Dictionary<string, ConcurrentDictionary<string, EdgeOutcome>> incomingEdgeStates)
    {
        if (!incomingEdgeStates.TryGetValue(targetId, out var parentStates))
        {
            return;
        }

        parentStates[parentId] = satisfied ? EdgeOutcome.Satisfied : EdgeOutcome.Unsatisfied;
    }

    private static void TryEnqueueNode(
        string nodeId,
        ConcurrentQueue<string> runQueue,
        ConcurrentDictionary<string, bool> enqueuedNodes,
        Dictionary<string, ConcurrentDictionary<string, EdgeOutcome>> incomingEdgeStates,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!incomingEdgeStates.TryGetValue(nodeId, out var parentStates))
        {
            if (enqueuedNodes.TryAdd(nodeId, true))
            {
                runQueue.Enqueue(nodeId);
            }

            return;
        }

        if (parentStates.Values.All(state => state != EdgeOutcome.Unknown) &&
            parentStates.Values.Any(state => state == EdgeOutcome.Satisfied))
        {
            if (enqueuedNodes.TryAdd(nodeId, true))
            {
                runQueue.Enqueue(nodeId);
            }
        }
    }

    private static bool EvaluateEdge(
        Edge edge,
        ActionExecutionStatus parentStatus,
        ConditionScope scope,
        JintConditionEvaluator evaluator)
    {
        var when = edge.When ?? "success";
        var whenSatisfied = when.ToLowerInvariant() switch
        {
            "always" => true,
            "success" => parentStatus == ActionExecutionStatus.Succeeded,
            "failure" => parentStatus == ActionExecutionStatus.Failed,
            _ => false
        };

        if (!whenSatisfied)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(edge.Condition))
        {
            return true;
        }

        return evaluator.Evaluate(edge.Condition, scope);
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        var policy = _options.RetryPolicy;
        var exponent = Math.Max(0, attemptNumber - 1);
        var initialMilliseconds = policy.InitialDelay.TotalMilliseconds;
        var scaledMilliseconds = initialMilliseconds * Math.Pow(policy.BackoffFactor, exponent);
        var baseDelay = TimeSpan.FromMilliseconds(scaledMilliseconds);

        var maxDelay = TimeSpan.FromTicks((long)(_options.DefaultActionTimeout.Ticks * 0.5));
        if (baseDelay > maxDelay)
        {
            baseDelay = maxDelay;
        }

        if (!policy.UseJitter)
        {
            return baseDelay;
        }

        var jitterMilliseconds = Random.Shared.NextDouble() * baseDelay.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(jitterMilliseconds);
    }

    private static Dictionary<string, object?> NormalizeParameters(Dictionary<string, object>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        return parameters.ToDictionary(
            static kvp => kvp.Key,
            static kvp => (object?)kvp.Value);
    }

    private static IReadOnlyDictionary<string, object?> CreateReadOnlyDictionary(Dictionary<string, object> source)
    {
        if (source.Count == 0)
        {
            return EmptyReadOnlyDictionary;
        }

        var copy = source.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        return new ReadOnlyDictionary<string, object?>(copy);
    }

    private static IReadOnlyDictionary<string, object?> CreateReadOnlyDictionaryFromOutputs(Dictionary<string, object?> source)
    {
        return source.Count == 0
            ? EmptyReadOnlyDictionary
            : new ReadOnlyDictionary<string, object?>(source);
    }

    private sealed record WorkflowRunOutcome(string Status);

    private enum EdgeOutcome
    {
        Unknown,
        Satisfied,
        Unsatisfied
    }
}
