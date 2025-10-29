using DataWorkflows.Engine.Core.Application.Orchestration;
using DataWorkflows.Engine.Core.Application.Registry;
using DataWorkflows.Engine.Core.Application.Templating;
using DataWorkflows.Engine.Core.Application.Evaluation;
using DataWorkflows.Engine.Core.Domain.Validation;
using DataWorkflows.Engine.Core.Domain.Parsing;
using DataWorkflows.Engine.Core.Configuration;
using DataWorkflows.Engine.Core.Services;
using DataWorkflows.Engine.Core.Validation;
using DataWorkflows.Engine.Infrastructure.Actions;
using DataWorkflows.Engine.Presentation.Services;
using DataWorkflows.Data.Migrations;
using DataWorkflows.Data.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DataWorkflows Engine API", Version = "v1" });
});

// Register configuration options
builder.Services.Configure<WorkflowCatalogOptions>(builder.Configuration.GetSection("WorkflowCatalog"));

// Register data repositories
var connectionString = builder.Configuration.GetConnectionString("Postgres")!;
builder.Services.AddSingleton<IActionCatalogRepository>(sp => new ActionCatalogRepository(connectionString));

// Register ActionCatalog services
builder.Services.AddSingleton<IActionCatalogRegistry, ActionCatalogRegistry>();
builder.Services.AddHostedService<ActionCatalogRegistryInitializer>();
builder.Services.AddSingleton<ISchemaValidator, SchemaValidator>();

// Register remote action executor
builder.Services.AddSingleton<IRemoteActionExecutor, RemoteActionExecutor>();

// Register application services
builder.Services.AddSingleton(provider =>
{
    var registry = new ActionRegistry();
    registry.Register(new CoreEchoAction());
    return registry;
});
builder.Services.AddSingleton(TemplateEngineOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
builder.Services.AddSingleton<IParameterValidator, ActionCatalogParameterValidator>();
builder.Services.AddSingleton<WorkflowParser>();
builder.Services.AddSingleton<GraphValidator>();
builder.Services.AddSingleton<JintConditionEvaluator>();
builder.Services.AddSingleton(provider =>
{
    var registry = provider.GetRequiredService<ActionRegistry>();
    var options = OrchestrationOptions.FromConfiguration(builder.Configuration);
    var templ = provider.GetRequiredService<ITemplateEngine>();
    var validator = provider.GetRequiredService<IParameterValidator>();
    var actionCatalogRegistry = provider.GetRequiredService<IActionCatalogRegistry>();
    var remoteActionExecutor = provider.GetRequiredService<IRemoteActionExecutor>();
    var logger = provider.GetRequiredService<ILogger<WorkflowConductor>>();
    return new WorkflowConductor(registry, options, templ, validator, actionCatalogRegistry, remoteActionExecutor, logger);
});

// Add HTTP client for calling connector services
builder.Services.AddHttpClient();

// Add health checks
builder.Services.AddHealthChecks()
      .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
      .AddNpgSql(
          connectionString: builder.Configuration.GetConnectionString("Postgres")!,
          name: "postgres",
          tags: new[] { "ready" }
      );

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataWorkflows Engine API v1"));
}

// Apply database migrations at startup
try
{
    var connStr = builder.Configuration.GetConnectionString("Postgres")!;
    await MigrationRunner.ApplyAll(connStr);
}
catch (Exception ex)
{
    Console.WriteLine($"Migration apply failed: {ex.Message}");
    throw;
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Map health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions {
    Predicate = check => check.Tags.Contains("live")
});

// app.MapHealthChecks("/health/ready", new HealthCheckOptions {
//     Predicate = check => check.Tags.Contains("ready")
// });

app.Run();
