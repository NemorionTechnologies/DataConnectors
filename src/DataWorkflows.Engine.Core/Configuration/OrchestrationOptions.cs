using System;
using Microsoft.Extensions.Configuration;

namespace DataWorkflows.Engine.Core.Configuration;

public sealed class OrchestrationOptions
{
    public int MaxParallelActions { get; set; } = 10;
    public TimeSpan DefaultActionTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan DefaultWorkflowTimeout { get; set; } = TimeSpan.FromHours(1);
    public RetryPolicyOptions RetryPolicy { get; set; } = new();

    public static OrchestrationOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new OrchestrationOptions();
        var section = configuration.GetSection("Orchestration");

        if (section.Exists())
        {
            section.Bind(options);
        }

        options.RetryPolicy = RetryPolicyOptions.FromConfiguration(configuration);

        if (options.DefaultActionTimeout <= TimeSpan.Zero)
        {
            options.DefaultActionTimeout = TimeSpan.FromMinutes(5);
        }

        if (options.DefaultWorkflowTimeout <= TimeSpan.Zero)
        {
            options.DefaultWorkflowTimeout = TimeSpan.FromHours(1);
        }

        return options;
    }
}