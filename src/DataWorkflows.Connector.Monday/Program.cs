using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Infrastructure;
using DataWorkflows.Connector.Monday.Presentation.Middleware;
using DataWorkflows.Connector.Monday.Actions;
using DataWorkflows.Connector.Monday.HostedServices;
using DataWorkflows.Contracts.Actions;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Monday Connector API", Version = "v1" });
});

// Configure guardrails
builder.Services.Configure<GuardrailOptions>(
    builder.Configuration.GetSection(GuardrailOptions.SectionName));

// Register workflow actions
builder.Services.AddScoped<IWorkflowAction, MondayGetItemsAction>();
builder.Services.AddScoped<IWorkflowAction, MondayGetSubItemsAction>();
builder.Services.AddScoped<IWorkflowAction, MondayGetItemUpdatesAction>();
builder.Services.AddScoped<IWorkflowAction, MondayGetBoardActivityAction>();
builder.Services.AddScoped<IWorkflowAction, MondayUpdateColumnAction>();

// Register action registration service
builder.Services.AddHostedService<ActionRegistrationService>();

// Add filter services (Dependency Inversion Principle: depend on abstractions)
builder.Services.AddSingleton<IMondayFilterGuardrailValidator, MondayFilterGuardrailValidator>();
builder.Services.AddSingleton<IMondayFilterTranslator, MondayFilterTranslator>();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Configure Polly retry policy with exponential backoff
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Add HTTP client with Polly for MondayApiClient
builder.Services.AddHttpClient<IMondayApiClient, MondayApiClient>()
    .AddPolicyHandler(retryPolicy);

// Add column resolution services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IColumnMetadataCache, InMemoryColumnMetadataCache>();
builder.Services.AddScoped<IColumnResolverService, ColumnResolverService>();

// Add column value parsing and filtering services
builder.Services.AddScoped<IColumnValueParser, ColumnValueParser>();
builder.Services.AddScoped<IItemFilterService, ItemFilterService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Monday Connector API v1"));
}

// Add exception handling middleware
app.UseExceptionHandler();

// Add correlation ID middleware
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the Program class public for integration testing
public partial class Program { }

