using DataWorkflows.Engine.Core.Application;
using DataWorkflows.Engine.Core.Interfaces;
using DataWorkflows.Engine.Orchestration;
using DataWorkflows.Engine.Registry;
using DataWorkflows.Engine.Templating;
using DataWorkflows.Engine.Validation;
using DataWorkflows.Engine.Configuration;
using DataWorkflows.Data.Migrations;
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

// Register application services
builder.Services.AddScoped<IWorkflowOrchestrator, WorkflowOrchestrator>();
builder.Services.AddSingleton<ActionRegistry>();
builder.Services.AddSingleton(TemplateEngineOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
builder.Services.AddSingleton<IParameterValidator, NoopParameterValidator>();
builder.Services.AddSingleton(provider =>
{
    var registry = provider.GetRequiredService<ActionRegistry>();
    var options = OrchestrationOptions.FromConfiguration(builder.Configuration);
    var templ = provider.GetRequiredService<ITemplateEngine>();
    var validator = provider.GetRequiredService<IParameterValidator>();
    var logger = provider.GetRequiredService<ILogger<WorkflowConductor>>();
    return new WorkflowConductor(registry, options, templ, validator, logger);
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
