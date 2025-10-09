using DataWorkflows.Contracts.Actions;
using DataWorkflows.Engine.Actions;

namespace DataWorkflows.Engine.Registry;

public class ActionRegistry
{
    private readonly Dictionary<string, IWorkflowAction> _actions = new();

    public ActionRegistry()
    {
        Register(new CoreEchoAction());
    }

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
