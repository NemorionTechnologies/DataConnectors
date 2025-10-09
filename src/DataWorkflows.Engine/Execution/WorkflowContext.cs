using System.Collections.Concurrent;

namespace DataWorkflows.Engine.Execution;

public sealed class WorkflowContext
{
    private readonly ConcurrentDictionary<string, object?> _data = new();

    public void SetActionOutput(string nodeId, object? output) => _data[nodeId] = output;

    public Dictionary<string, object?> GetAllOutputs() =>
        _data.ToDictionary(kv => kv.Key, kv => kv.Value);
}
