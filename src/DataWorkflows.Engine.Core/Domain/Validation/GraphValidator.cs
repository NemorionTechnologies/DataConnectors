using System;
using System.Collections.Generic;
using System.Linq;
using DataWorkflows.Engine.Core.Domain.Models;

namespace DataWorkflows.Engine.Core.Domain.Validation;

public sealed class GraphValidator
{
    public void Validate(WorkflowDefinition workflow)
    {
        if (workflow.Nodes is null || workflow.Nodes.Count == 0)
        {
            throw new ArgumentException("Workflow must define at least one node.");
        }

        var nodeIndex = workflow.Nodes.ToDictionary(node => node.Id);

        if (!nodeIndex.ContainsKey(workflow.StartNode))
        {
            throw new ArgumentException($"startNode '{workflow.StartNode}' not found in nodes.");
        }

        // Validate all edge targets exist
        foreach (var node in workflow.Nodes)
        {
            if (node.Edges is null)
            {
                continue;
            }

            foreach (var edge in node.Edges)
            {
                if (!nodeIndex.ContainsKey(edge.TargetNode))
                {
                    throw new ArgumentException($"Edge target '{edge.TargetNode}' not found (from node '{node.Id}').");
                }
            }
        }

        // Detect cycles using DFS
        var visited = new HashSet<string>();
        var recursion = new HashSet<string>();

        bool HasCycle(string nodeId)
        {
            if (recursion.Contains(nodeId))
            {
                return true;
            }

            if (visited.Contains(nodeId))
            {
                return false;
            }

            visited.Add(nodeId);
            recursion.Add(nodeId);

            if (nodeIndex[nodeId].Edges is { Count: > 0 } edges)
            {
                foreach (var edge in edges)
                {
                    if (HasCycle(edge.TargetNode))
                    {
                        return true;
                    }
                }
            }

            recursion.Remove(nodeId);
            return false;
        }

        if (HasCycle(workflow.StartNode))
        {
            throw new ArgumentException("Workflow contains a cycle (not a DAG).");
        }

        // Ensure reachability from start node
        var reachable = new HashSet<string>();

        void Traverse(string nodeId)
        {
            if (!reachable.Add(nodeId))
            {
                return;
            }

            if (nodeIndex[nodeId].Edges is { Count: > 0 } edges)
            {
                foreach (var edge in edges)
                {
                    Traverse(edge.TargetNode);
                }
            }
        }

        Traverse(workflow.StartNode);

        if (reachable.Count != nodeIndex.Count)
        {
            var unreachable = nodeIndex.Keys.Except(reachable);
            throw new ArgumentException($"Workflow contains unreachable nodes: {string.Join(", ", unreachable)}");
        }
    }
}

