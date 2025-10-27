using System;
using Microsoft.Extensions.Configuration;

namespace DataWorkflows.Engine.Core.Application.Templating;

public sealed class TemplateEngineOptions
{
    public int RenderTimeoutMs { get; set; } = 2000;
    public bool StrictMode { get; set; } = true;
    public bool EnableLoops { get; set; } = false;
    public bool EnableFunctions { get; set; } = false;

    public static TemplateEngineOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new TemplateEngineOptions();
        var section = configuration.GetSection("TemplateEngineOptions");
        if (section.Exists())
        {
            section.Bind(options);
        }

        if (options.RenderTimeoutMs <= 0)
        {
            options.RenderTimeoutMs = 2000;
        }

        return options;
    }
}

