using System;
using Microsoft.Extensions.Configuration;

namespace DataWorkflows.Engine.Core.Configuration;

public sealed class RetryPolicyOptions
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public double BackoffFactor { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;

    public static RetryPolicyOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new RetryPolicyOptions();
        var section = configuration.GetSection("Orchestration:RetryPolicy");

        if (section.Exists())
        {
            section.Bind(options);
        }

        if (options.MaxAttempts < 1)
        {
            options.MaxAttempts = 1;
        }

        if (options.InitialDelay <= TimeSpan.Zero)
        {
            options.InitialDelay = TimeSpan.FromMilliseconds(50);
        }

        if (options.BackoffFactor < 1.0)
        {
            options.BackoffFactor = 1.0;
        }

        return options;
    }
}