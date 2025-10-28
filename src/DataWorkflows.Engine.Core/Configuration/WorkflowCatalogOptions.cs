using Microsoft.Extensions.Configuration;

namespace DataWorkflows.Engine.Core.Configuration;

public sealed class WorkflowCatalogOptions
{
    public bool AutoRegisterActionsOnStartup { get; init; } = true;
    public bool ValidateActionSchemasOnStartup { get; init; } = true;
    public bool AllowDraftExecution { get; init; } = false;

    public static WorkflowCatalogOptions FromConfiguration(IConfigurationSection section)
    {
        var options = new WorkflowCatalogOptions();
        section.Bind(options);
        return options;
    }
}
