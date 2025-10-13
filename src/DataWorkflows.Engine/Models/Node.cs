using System.Collections.Generic;

namespace DataWorkflows.Engine.Models;

public sealed record Node(
    string Id,
    string ActionType,
    Dictionary<string, object>? Parameters = null,
    List<Edge>? Edges = null,
    string RoutePolicy = "parallel"
);
