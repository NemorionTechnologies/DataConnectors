using DataWorkflows.Contracts.Actions;

namespace DataWorkflows.Engine.Core.Application.Registry;

public class ActionRegistry
{
    private readonly Dictionary<string, IWorkflowAction> _actions = new();

    public void Register(IWorkflowAction action)
    {
        _actions[action.Type] = action;
    }

    public IWorkflowAction GetAction(string actionType)
    {
        return _actions.TryGetValue(actionType, out var action)
            ? action
            : throw new KeyNotFoundException($"Action not found: {actionType}");
    }
}
