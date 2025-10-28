using System.Collections.Generic;

namespace DataWorkflows.Engine.Core.Domain.Models;

public sealed record Node(
    string Id,
    string ActionType,
    Dictionary<string, object>? Parameters = null,
    List<Edge>? Edges = null,
    string RoutePolicy = "parallel",
    NodePolicies? Policies = null,
    string? OnFailure = null
);

public sealed record NodePolicies(
    bool RerenderOnRetry = false
);
