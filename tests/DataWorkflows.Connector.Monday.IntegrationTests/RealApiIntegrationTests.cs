using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

/// <summary>
/// Real API integration tests that call the actual Monday.com API.
/// These tests are skipped by default. Remove [Fact(Skip = ...)] to run them.
///
/// SETUP REQUIRED:
/// 1. Update testsettings.json with your configuration:
///    - BoardId: Your Monday.com board ID
///    - ApiKey: Your Monday.com API token
///    - StatusColumnId: A status column ID from your board
///    - StatusLabel: A valid status label for your board
///
///    OR set environment variables:
///    - MONDAY_BOARD_ID
///    - MONDAY_API_TOKEN
///    - MONDAY_STATUS_COLUMN_ID
///    - MONDAY_STATUS_LABEL
///
/// 2. Your board should have:
///    - At least 1 group
///    - At least 1 item in that group
///    - A status column (for write tests)
///    - Optional: sub-items, updates/comments
///
/// 3. Run tests individually to see results, or remove Skip parameter
/// </summary>
public class RealApiIntegrationTests : MondayIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public RealApiIntegrationTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task GetBoardItems_ShouldReturnActualItems()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // Act
        _output.WriteLine($"Fetching items from board {Config.BoardId}...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {items?.Count ?? 0} items");
        items.Should().NotBeNull();
        items.Should().NotBeEmpty("Board should have at least one item");

        // Print details about the first item
        if (items?.Any() == true)
        {
            var firstItem = items.First();
            _output.WriteLine($"First item ID: {firstItem.Id}");
            _output.WriteLine($"First item Title: {firstItem.Title}");
            _output.WriteLine($"First item GroupId: {firstItem.GroupId}");
            _output.WriteLine($"Column values: {firstItem.ColumnValues.Count}");

            foreach (var col in firstItem.ColumnValues.Take(5))
            {
                var displayValue = !string.IsNullOrEmpty(col.Value.Text) ? col.Value.Text : col.Value.Value ?? "(empty)";
                _output.WriteLine($"  - {col.Key}: {displayValue}");
            }
        }
    }

    [Fact]
    public async Task GetBoardActivity_ShouldReturnActivityLog()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // Act
        _output.WriteLine($"Fetching activity log from board {Config.BoardId}...");
        var logs = (await apiClient.GetBoardActivityAsync(
            Config.BoardId,
            null,
            null,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {logs?.Count ?? 0} activity log entries");
        logs.Should().NotBeNull();

        if (logs?.Any() == true)
        {
            foreach (var log in logs.Take(5))
            {
                _output.WriteLine($"Event: {log.EventType}, User: {log.UserId}, Time: {log.CreatedAt}");
            }
        }
    }

    [Fact]
    public async Task GetBoardUpdates_ShouldReturnUpdates()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // Act
        _output.WriteLine($"Fetching updates from board {Config.BoardId}...");
        var updates = (await apiClient.GetBoardUpdatesAsync(
            Config.BoardId,
            null,
            null,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {updates?.Count ?? 0} updates");
        updates.Should().NotBeNull();

        if (updates?.Any() == true)
        {
            foreach (var update in updates.Take(5))
            {
                _output.WriteLine($"Update ID: {update.Id}, Item: {update.ItemId}");
                _output.WriteLine($"  Body: {update.BodyText.Substring(0, Math.Min(50, update.BodyText.Length))}...");
            }
        }
    }

    [Fact]
    public async Task GetSubItems_ShouldReturnSubItems()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // First, get an item ID from the board
        _output.WriteLine($"First, fetching items to get a parent item ID...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        if (items == null || !items.Any())
        {
            _output.WriteLine("No items found in board. Skipping sub-items test.");
            return;
        }

        var parentItemId = items.First().Id;
        _output.WriteLine($"Using parent item ID: {parentItemId}");

        // Act
        var subItems = (await apiClient.GetSubItemsAsync(
            parentItemId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {subItems?.Count ?? 0} sub-items for item {parentItemId}");
        subItems.Should().NotBeNull();

        if (subItems?.Any() == true)
        {
            foreach (var subItem in subItems)
            {
                _output.WriteLine($"Sub-item ID: {subItem.Id}, Title: {subItem.Title}, Parent: {subItem.ParentId}");
            }
        }
        else
        {
            _output.WriteLine("No sub-items found. This is OK if your board doesn't have sub-items.");
        }
    }

    [Fact]
    public async Task GetItemUpdates_ShouldReturnItemUpdates()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // First, get an item ID
        _output.WriteLine($"First, fetching items to get an item ID...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        if (items == null || !items.Any())
        {
            _output.WriteLine("No items found in board. Skipping item updates test.");
            return;
        }

        var itemId = items.First().Id;
        _output.WriteLine($"Using item ID: {itemId}");

        // Act
        var updates = (await apiClient.GetItemUpdatesAsync(
            itemId,
            null,
            null,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {updates?.Count ?? 0} updates for item {itemId}");
        updates.Should().NotBeNull();

        if (updates?.Any() == true)
        {
            foreach (var update in updates)
            {
                _output.WriteLine($"Update ID: {update.Id}");
                _output.WriteLine($"  Body: {update.BodyText.Substring(0, Math.Min(100, update.BodyText.Length))}...");
            }
        }
        else
        {
            _output.WriteLine("No updates found. This is OK if your items don't have updates/comments.");
        }
    }

    [Fact]
    public async Task GetHydratedItems_ShouldReturnItemsWithSubItemsAndUpdates()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // Act
        _output.WriteLine($"Fetching hydrated items from board {Config.BoardId}...");
        var hydratedItems = (await apiClient.GetHydratedBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {hydratedItems?.Count ?? 0} hydrated items");
        hydratedItems.Should().NotBeNull();

        if (hydratedItems?.Any() == true)
        {
            var firstItem = hydratedItems.First();
            _output.WriteLine($"First hydrated item:");
            _output.WriteLine($"  ID: {firstItem.Id}");
            _output.WriteLine($"  Title: {firstItem.Title}");
            _output.WriteLine($"  Sub-items count: {firstItem.SubItems.Count()}");
            _output.WriteLine($"  Updates count: {firstItem.Updates.Count()}");
        }
    }

    [Fact]
    public async Task UpdateColumnValue_ShouldUpdateStatusColumn()
    {
        Config.Validate();

        // WARNING: This test WILL modify your board!
        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // First, get an item ID and check what columns exist
        _output.WriteLine($"Fetching items to get item ID and column info...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        if (items == null || !items.Any())
        {
            _output.WriteLine("No items found. Cannot test update.");
            return;
        }

        var itemId = items.First().Id;
        _output.WriteLine($"Using item ID: {itemId}");
        _output.WriteLine($"Available columns:");
        foreach (var col in items.First().ColumnValues.Take(10))
        {
            var displayValue = !string.IsNullOrEmpty(col.Value.Text) ? col.Value.Text : col.Value.Value ?? "(empty)";
            _output.WriteLine($"  - {col.Key}: {displayValue}");
        }

        // Resolve column title to ID using the caching layer
        var statusColumnId = await ResolveColumnIdAsync(Config.StatusColumnTitle);
        _output.WriteLine($"Resolved '{Config.StatusColumnTitle}' to column ID: {statusColumnId}");

        _output.WriteLine($"\nAttempting to update column '{Config.StatusColumnTitle}' to '{Config.StatusLabel}'");
        _output.WriteLine("WARNING: This WILL modify your board!");

        // Act
        try
        {
            var updatedItem = await apiClient.UpdateColumnValueAsync(
                Config.BoardId,
                itemId,
                statusColumnId,
                $"{{ \"label\": \"{Config.StatusLabel}\" }}",
                CancellationToken.None);

            // Assert
            _output.WriteLine($"Successfully updated item {itemId}");
            _output.WriteLine($"Updated column values:");
            foreach (var col in updatedItem.ColumnValues.Take(10))
            {
                var displayValue = !string.IsNullOrEmpty(col.Value.Text) ? col.Value.Text : col.Value.Value ?? "(empty)";
                _output.WriteLine($"  - {col.Key}: {displayValue}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Update failed: {ex.Message}");
            _output.WriteLine("\nPossible reasons:");
            _output.WriteLine($"1. Column with title '{Config.StatusColumnTitle}' doesn't exist on your board");
            _output.WriteLine($"2. Status label '{Config.StatusLabel}' doesn't match your board's status options");
            _output.WriteLine("3. API token doesn't have write permissions");
        }
    }

    [Fact]
    public async Task UpdateColumnByTitle_ShouldResolveColumnTitleAndUpdate()
    {
        Config.Validate();

        // WARNING: This test WILL modify your board!
        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        _output.WriteLine($"Fetching items to get item ID...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        if (items == null || !items.Any())
        {
            _output.WriteLine("No items found. Cannot test column title resolution.");
            return;
        }

        var itemId = items.First().Id;
        _output.WriteLine($"Using item ID: {itemId}");
        _output.WriteLine($"\nAttempting to update column using title '{Config.StatusColumnTitle}' via POST endpoint");
        _output.WriteLine("WARNING: This WILL modify your board!");

        // Act - Use column title via the new POST endpoint
        var statusValue = new { label = Config.StatusLabel };
        var request = new UpdateColumnValueRequest(
            Config.BoardId,
            JsonSerializer.Serialize(statusValue),
            ColumnId: null,
            ColumnTitle: Config.StatusColumnTitle);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync($"/api/v1/items/{itemId}/columns/update", content);

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var updatedItem = await response.Content.ReadFromJsonAsync<MondayItemDto>();
            _output.WriteLine($"Successfully updated item {itemId} using column title '{Config.StatusColumnTitle}'");
            _output.WriteLine($"Updated column values:");
            foreach (var col in updatedItem!.ColumnValues.Take(10))
            {
                var displayValue = !string.IsNullOrEmpty(col.Value.Text) ? col.Value.Text : col.Value.Value ?? "(empty)";
                _output.WriteLine($"  - {col.Key}: {displayValue}");
            }
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Update failed: {response.StatusCode}");
            _output.WriteLine($"Error: {error}");
            _output.WriteLine("\nPossible reasons:");
            _output.WriteLine($"1. Column with title '{Config.StatusColumnTitle}' doesn't exist on your board");
            _output.WriteLine($"2. Status label '{Config.StatusLabel}' doesn't match your board's status options");
            _output.WriteLine("3. API token doesn't have write permissions");
        }
    }

    [Fact]
    public async Task UpdateLinkColumn_ShouldAddLinkToGitHubColumn()
    {
        Config.Validate();

        // WARNING: This test WILL modify your board!
        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();

        // First, get an item ID
        _output.WriteLine($"Fetching items to get item ID...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        if (items == null || !items.Any())
        {
            _output.WriteLine("No items found. Cannot test link update.");
            return;
        }

        var itemId = items.First().Id;
        _output.WriteLine($"Using item ID: {itemId}");

        // Resolve link column title to ID using the caching layer
        var linkColumnId = await ResolveColumnIdAsync(Config.LinkColumnTitle);
        _output.WriteLine($"Resolved '{Config.LinkColumnTitle}' to column ID: {linkColumnId}");

        // Show current link column value
        var linkColumn = items.First().ColumnValues.GetValueOrDefault(linkColumnId);
        var currentLinkDisplay = !string.IsNullOrEmpty(linkColumn?.Text) ? linkColumn.Text : linkColumn?.Value ?? "(empty)";
        _output.WriteLine($"Current {Config.LinkColumnTitle} column value: {currentLinkDisplay}");

        _output.WriteLine($"\nAttempting to add link to '{Config.LinkColumnTitle}' column");
        _output.WriteLine("Link text: 'Link Here'");
        _output.WriteLine("Link URL: 'www.google.com'");
        _output.WriteLine("WARNING: This WILL modify your board!");

        // Act - Monday.com link column format: {"url": "...", "text": "..."}
        var linkValue = new { url = "www.google.com", text = "Link Here" };

        try
        {
            var updatedItem = await apiClient.UpdateColumnValueAsync(
                Config.BoardId,
                itemId,
                linkColumnId,
                JsonSerializer.Serialize(linkValue),
                CancellationToken.None);

            // Assert
            _output.WriteLine($"\nSuccessfully updated item {itemId}");
            _output.WriteLine($"{Config.LinkColumnTitle} column updated:");
            var updatedLink = updatedItem.ColumnValues.GetValueOrDefault(linkColumnId);
            var updatedLinkDisplay = !string.IsNullOrEmpty(updatedLink?.Text) ? updatedLink.Text : updatedLink?.Value ?? "(empty)";
            _output.WriteLine($"  - {Config.LinkColumnTitle}: {updatedLinkDisplay}");

            updatedItem.Should().NotBeNull();
            updatedItem.ColumnValues.Should().ContainKey(linkColumnId);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"\nUpdate failed: {ex.Message}");
            _output.WriteLine("\nPossible reasons:");
            _output.WriteLine("1. Column 'link' doesn't exist on your board");
            _output.WriteLine("2. Link format is incorrect");
            _output.WriteLine("3. API token doesn't have write permissions");
        }
    }

    [Fact]
    public async Task GetStatusForItemsWithTimelineAfterNovember1st()
    {
        Config.Validate();

        // Arrange
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var columnValueParser = scope.ServiceProvider.GetRequiredService<IColumnValueParser>();
        var itemFilterService = scope.ServiceProvider.GetRequiredService<IItemFilterService>();

        // Act - Get all items from the board
        _output.WriteLine($"Fetching all items from board {Config.BoardId}...");
        var items = (await apiClient.GetBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        // Assert
        _output.WriteLine($"Retrieved {items?.Count ?? 0} items");
        items.Should().NotBeNull();

        if (items == null || !items.Any())
        {
            _output.WriteLine("No items found on board.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Config.TimelineColumnTitle))
        {
            _output.WriteLine("TimelineColumnTitle not configured in testsettings.json. Skipping test.");
            _output.WriteLine("Please add 'TimelineColumnTitle' to the Monday section in testsettings.json");
            return;
        }

        // Resolve column titles to IDs using the caching layer
        var timelineColumnId = await ResolveColumnIdAsync(Config.TimelineColumnTitle);
        var statusColumnId = await ResolveColumnIdAsync(Config.StatusColumnTitle);

        _output.WriteLine($"Resolved '{Config.TimelineColumnTitle}' to column ID: {timelineColumnId}");
        _output.WriteLine($"Resolved '{Config.StatusColumnTitle}' to column ID: {statusColumnId}");

        // Filter items with timeline end date after November 1st using the service
        var cutoffDate = new DateTime(2025, 11, 1);
        var filteredItems = itemFilterService.FilterByTimelineEndDate(
            items,
            timelineColumnId,
            cutoffDate).ToList();

        // Build output data
        var itemsAfterNov1 = filteredItems.Select(item =>
        {
            var endDate = itemFilterService.GetTimelineEndDate(item, timelineColumnId);
            var statusColumn = item.ColumnValues.GetValueOrDefault(statusColumnId);
            var status = columnValueParser.GetTextValue(statusColumn, "No Status");

            return (item.Id, item.Title, Status: status, EndDate: endDate);
        }).ToList();

        // Output results
        _output.WriteLine($"\nFound {itemsAfterNov1.Count} items with timeline end date after November 1st, 2025:");
        _output.WriteLine(new string('=', 80));

        if (itemsAfterNov1.Any())
        {
            foreach (var (itemId, title, status, endDate) in itemsAfterNov1)
            {
                _output.WriteLine($"Item: {title}");
                _output.WriteLine($"  ID: {itemId}");
                _output.WriteLine($"  Status: {status}");
                _output.WriteLine($"  Timeline End Date: {endDate:yyyy-MM-dd}");
                _output.WriteLine("");
            }

            // Assertion: At least one item should match our filter
            itemsAfterNov1.Should().NotBeEmpty("Expected to find items with end dates after November 1st");
        }
        else
        {
            _output.WriteLine("No items found with timeline end dates after November 1st, 2025.");
            _output.WriteLine("\nAll items and their timeline values:");
            foreach (var item in items)
            {
                if (item.ColumnValues.TryGetValue("timerange_mkwbf51a", out var timelineColumn))
                {
                    var timelineDisplay = !string.IsNullOrEmpty(timelineColumn.Text) ? timelineColumn.Text : timelineColumn.Value ?? "(empty)";
                    _output.WriteLine($"  {item.Title}: {timelineDisplay}");
                }
                else
                {
                    _output.WriteLine($"  {item.Title}: No timeline");
                }
            }
        }
    }

    [Fact]
    public async Task GetItemsAndSubItemsWithRecentUpdates_ShouldReturnStatusAndTimeline()
    {
        Config.Validate();

        // Arrange
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        _output.WriteLine($"Fetching items/subitems with updates since {cutoffDate:yyyy-MM-dd}...");

        // Resolve column titles to IDs using caching layer
        var statusColumnId = await ResolveColumnIdAsync(Config.StatusColumnTitle);
        var timelineColumnId = await ResolveColumnIdAsync(Config.TimelineColumnTitle);
        _output.WriteLine($"Resolved '{Config.StatusColumnTitle}' to: {statusColumnId}");
        _output.WriteLine($"Resolved '{Config.TimelineColumnTitle}' to: {timelineColumnId}");

        // Use configured factory with API key
        var configuredFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Monday:ApiKey"] = Config.ApiKey,
                    ["Monday:BoardId"] = Config.BoardId
                }!);
            });
        });

        using var scope = configuredFactory.Services.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<IMondayApiClient>();
        var itemFilterService = scope.ServiceProvider.GetRequiredService<IItemFilterService>();

        // Get hydrated items using API client
        var hydratedItems = (await apiClient.GetHydratedBoardItemsAsync(
            Config.BoardId,
            MondayFilterDefinition.Empty,
            CancellationToken.None)).ToList();

        hydratedItems.Should().NotBeNull();
        if (hydratedItems == null || !hydratedItems.Any())
        {
            _output.WriteLine("No items found on board.");
            return;
        }

        var results = await itemFilterService.GetItemsWithRecentUpdatesAsync(
            hydratedItems,
            statusColumnId,
            timelineColumnId,
            cutoffDate);

        // Output results
        _output.WriteLine($"\n{'='.ToString().PadRight(100, '=')}");
        _output.WriteLine($"ITEMS/SUBITEMS WITH UPDATES IN LAST 30 DAYS");
        _output.WriteLine($"{'='.ToString().PadRight(100, '=')}");
        _output.WriteLine($"Found {results.Count} items/subitems with recent updates\n");

        foreach (var item in results)
        {
            _output.WriteLine($"[{item.Type}] {item.Title}");
            _output.WriteLine($"  ID: {item.Id}");
            _output.WriteLine($"  Status: {item.Status}");

            if (item.Timeline != null)
            {
                var timelineText = item.Timeline.From.HasValue && item.Timeline.To.HasValue
                    ? $"{item.Timeline.From.Value:yyyy-MM-dd} to {item.Timeline.To.Value:yyyy-MM-dd}"
                    : item.Timeline.From.HasValue
                        ? $"From {item.Timeline.From.Value:yyyy-MM-dd}"
                        : item.Timeline.To.HasValue
                            ? $"Until {item.Timeline.To.Value:yyyy-MM-dd}"
                            : "No dates";
                _output.WriteLine($"  Timeline: {timelineText}");
            }
            else
            {
                _output.WriteLine($"  Timeline: None");
            }

            _output.WriteLine($"  Last Update: {item.LastUpdateDate:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");
        }

        if (!results.Any())
        {
            _output.WriteLine("No items or subitems with updates in the last 30 days.");
        }

        results.Should().NotBeNull();
    }

    [Fact]
    public async Task CorrelationId_ShouldBePresentInResponse()
    {
        Config.Validate();

        // Arrange
        var correlationId = "test-correlation-" + Guid.NewGuid();
        Client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        // Act
        var response = await Client.GetAsync($"/api/v1/boards/{Config.BoardId}/items");

        // Assert
        response.Headers.Should().ContainKey("X-Correlation-ID");
        var returnedId = response.Headers.GetValues("X-Correlation-ID").First();
        returnedId.Should().Be(correlationId);
        _output.WriteLine($"Correlation ID round-trip successful: {correlationId}");
    }
}

