using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Connector.Monday.Actions.Schemas;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.HostedServices;

/// <summary>
/// Background service that registers Monday connector actions with the workflow engine on startup.
/// Retries with exponential backoff if the engine is not available.
/// </summary>
public sealed class ActionRegistrationService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ActionRegistrationService> _logger;
    private readonly string _engineUrl;

    public ActionRegistrationService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ActionRegistrationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Read engine URL from environment or configuration
        _engineUrl = Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_URL")
                     ?? _configuration["WorkflowEngine:Url"]
                     ?? "http://localhost:5131";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ActionRegistrationService starting. Engine URL: {EngineUrl}", _engineUrl);

        // Wait a bit for the service to fully start
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var attempt = 0;
        var maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(2);

        while (!stoppingToken.IsCancellationRequested && attempt < maxAttempts)
        {
            attempt++;

            try
            {
                _logger.LogInformation("Attempting to register actions with workflow engine (attempt {Attempt}/{MaxAttempts})", attempt, maxAttempts);

                await RegisterActionsAsync(stoppingToken);

                _logger.LogInformation("✓ Successfully registered actions with workflow engine!");
                return; // Success - exit the loop
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register actions (attempt {Attempt}/{MaxAttempts}): {Message}", attempt, maxAttempts, ex.Message);

                if (attempt < maxAttempts)
                {
                    _logger.LogInformation("Retrying in {Delay} seconds...", delay.TotalSeconds);
                    await Task.Delay(delay, stoppingToken);

                    // Exponential backoff
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
                }
                else
                {
                    _logger.LogError("Failed to register actions after {MaxAttempts} attempts. Connector will run but actions won't be available in workflows.", maxAttempts);
                }
            }
        }
    }

    private async Task RegisterActionsAsync(CancellationToken cancellationToken)
    {
        // Build registration payload
        var registration = new
        {
            connectorId = "monday",
            actions = new[]
            {
                new
                {
                    actionType = "monday.get-items",
                    displayName = "Get Board Items",
                    description = "Retrieve items from a Monday.com board with optional filtering",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetItemsParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetItemsOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.get-subitems",
                    displayName = "Get Sub-Items",
                    description = "Retrieve sub-items from a Monday.com parent item with optional filtering",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetSubItemsParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetSubItemsOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.get-item-updates",
                    displayName = "Get Item Updates",
                    description = "Retrieve updates from a Monday.com item with optional date filtering",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetItemUpdatesParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetItemUpdatesOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.get-board-activity",
                    displayName = "Get Board Activity",
                    description = "Retrieve activity log from a Monday.com board with optional date filtering",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetBoardActivityParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetBoardActivityOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.update-column",
                    displayName = "Update Column Value",
                    description = "Update a column value on a Monday.com item",
                    parameterSchema = SchemaGenerator.GenerateSchema<UpdateColumnParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<UpdateColumnOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.update-subitem-column",
                    displayName = "Update Sub-Item Column Value",
                    description = "Update a column value on a Monday.com sub-item (cosmetic alias for user clarity - uses same infrastructure as update-column)",
                    parameterSchema = SchemaGenerator.GenerateSchema<UpdateSubItemColumnParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<UpdateSubItemColumnOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.create-item",
                    displayName = "Create Board Item",
                    description = "Create a new item on a Monday.com board",
                    parameterSchema = SchemaGenerator.GenerateSchema<CreateItemParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<CreateItemOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.create-subitem",
                    displayName = "Create Sub-Item",
                    description = "Create a new sub-item under a parent item in Monday.com",
                    parameterSchema = SchemaGenerator.GenerateSchema<CreateSubItemParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<CreateSubItemOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.get-board-columns",
                    displayName = "Get Board Columns",
                    description = "Retrieve column metadata from a Monday.com board",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetBoardColumnsParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetBoardColumnsOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.get-board-updates",
                    displayName = "Get Board Updates",
                    description = "Retrieve all updates from a Monday.com board with optional date filtering",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetBoardUpdatesParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetBoardUpdatesOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                },
                new
                {
                    actionType = "monday.get-items-with-details",
                    displayName = "Get Items with Details",
                    description = "Retrieve items with their sub-items and updates included (saves multiple API calls)",
                    parameterSchema = SchemaGenerator.GenerateSchema<GetItemsWithDetailsParameters>(),
                    outputSchema = SchemaGenerator.GenerateSchema<GetItemsWithDetailsOutput>(),
                    requiresAuth = true,
                    isEnabled = true
                }
            }
        };

        // Send registration request to engine
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var registrationUrl = $"{_engineUrl.TrimEnd('/')}/api/v1/admin/actions/register";
        var json = JsonSerializer.Serialize(registration, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending registration request to {Url}:\n{Json}", registrationUrl, json);

        var response = await httpClient.PostAsync(registrationUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Failed to register actions. Status: {response.StatusCode}, Response: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("Registration response: {Response}", responseContent);
    }
}
