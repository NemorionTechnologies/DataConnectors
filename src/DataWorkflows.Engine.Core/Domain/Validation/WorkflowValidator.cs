using DataWorkflows.Engine.Core.Application.Evaluation;
using DataWorkflows.Engine.Core.Application.Registry;
using DataWorkflows.Engine.Core.Domain.Models;
using DataWorkflows.Engine.Core.Services;

namespace DataWorkflows.Engine.Core.Domain.Validation;

public class WorkflowValidator
{
    private readonly GraphValidator _graphValidator;
    private readonly ActionRegistry _actionRegistry;
    private readonly IActionCatalogRegistry _actionCatalogRegistry;
    private readonly JintConditionEvaluator _conditionEvaluator;

    public WorkflowValidator(
        GraphValidator graphValidator,
        ActionRegistry actionRegistry,
        IActionCatalogRegistry actionCatalogRegistry,
        JintConditionEvaluator conditionEvaluator)
    {
        _graphValidator = graphValidator;
        _actionRegistry = actionRegistry;
        _actionCatalogRegistry = actionCatalogRegistry;
        _conditionEvaluator = conditionEvaluator;
    }

    public ValidationResult Validate(WorkflowDefinition workflow)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 1. JSON Schema validation (already done during deserialization)
        // 2. Cross-reference checks (graph validation)
        try
        {
            _graphValidator.Validate(workflow);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Graph validation failed: {ex.Message}");
            // Return early if graph is invalid, don't proceed with other checks
            return new ValidationResult(false, errors, warnings);
        }

        // 3. Action availability check (check both local and remote actions)
        foreach (var node in workflow.Nodes)
        {
            if (node.ActionType != null)
            {
                // Try local ActionRegistry first
                bool foundLocal = false;
                try
                {
                    _actionRegistry.GetAction(node.ActionType);
                    foundLocal = true;
                }
                catch (KeyNotFoundException)
                {
                    // Not a local action, check ActionCatalogRegistry for remote actions
                }

                if (!foundLocal)
                {
                    // Check ActionCatalogRegistry for remote actions
                    var catalogEntry = _actionCatalogRegistry.GetAction(node.ActionType);
                    if (catalogEntry == null || !catalogEntry.IsEnabled)
                    {
                        errors.Add($"Action '{node.ActionType}' not found or disabled (node '{node.Id}')");
                    }
                }
            }
        }

        // 4. Jint condition syntax check
        foreach (var node in workflow.Nodes)
        {
            if (node.Edges != null)
            {
                foreach (var edge in node.Edges)
                {
                    if (!string.IsNullOrWhiteSpace(edge.Condition))
                    {
                        try
                        {
                            // Pre-compile condition to validate syntax
                            _conditionEvaluator.Evaluate(edge.Condition, Application.Evaluation.ConditionScope.Empty);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Invalid condition syntax in node '{node.Id}' edge to '{edge.TargetNode}': {ex.Message}");
                        }
                    }
                }
            }

            // Also check onFailure node reference if present
            if (node.OnFailure != null)
            {
                if (!workflow.Nodes.Any(n => n.Id == node.OnFailure))
                {
                    errors.Add($"OnFailure target '{node.OnFailure}' not found (node '{node.Id}')");
                }
            }
        }

        // 5. Additional validations
        // Check for duplicate node IDs
        var duplicateNodeIds = workflow.Nodes
            .GroupBy(n => n.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicateId in duplicateNodeIds)
        {
            errors.Add($"Duplicate node ID: '{duplicateId}'");
        }

        // Check for unreachable nodes (warning only)
        var reachableNodes = GetReachableNodes(workflow);
        var unreachableNodes = workflow.Nodes
            .Where(n => !reachableNodes.Contains(n.Id))
            .Select(n => n.Id)
            .ToList();

        foreach (var unreachableId in unreachableNodes)
        {
            warnings.Add($"Node '{unreachableId}' is unreachable from startNode");
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    private HashSet<string> GetReachableNodes(WorkflowDefinition workflow)
    {
        var reachable = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(workflow.StartNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (reachable.Contains(current))
                continue;

            reachable.Add(current);

            var node = workflow.Nodes.FirstOrDefault(n => n.Id == current);
            if (node?.Edges != null)
            {
                foreach (var edge in node.Edges)
                {
                    if (!reachable.Contains(edge.TargetNode))
                    {
                        queue.Enqueue(edge.TargetNode);
                    }
                }
            }

            // Also check onFailure targets
            if (node?.OnFailure != null && !reachable.Contains(node.OnFailure))
            {
                queue.Enqueue(node.OnFailure);
            }
        }

        return reachable;
    }
}

public record ValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings
);
