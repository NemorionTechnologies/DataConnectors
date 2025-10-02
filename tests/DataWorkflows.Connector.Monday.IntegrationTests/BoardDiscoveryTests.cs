using System.Net.Http.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

/// <summary>
/// Helper tests to discover your Monday.com board structure.
/// Run these FIRST to understand what's in your board before running other tests.
///
/// SETUP:
/// 1. Update testsettings.json with your Board ID and API token
///    OR set environment variables: MONDAY_BOARD_ID and MONDAY_API_TOKEN
/// 2. Remove [Fact(Skip = ...)] to run
/// 3. Run test and check output to see your board structure
/// </summary>
public class BoardDiscoveryTests : MondayIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public BoardDiscoveryTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task DiscoverBoardStructure_ShowsCompleteDetails()
    {
        Config.Validate();

        _output.WriteLine("=".PadRight(80, '='));
        _output.WriteLine($"DISCOVERING STRUCTURE FOR BOARD: {Config.BoardId}");
        _output.WriteLine("=".PadRight(80, '='));

        // Get board items
        _output.WriteLine("\nFETCHING BOARD ITEMS...");
        var response = await Client.GetAsync($"/api/v1/boards/{Config.BoardId}/items");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"\nERROR: {response.StatusCode}");
            _output.WriteLine($"Response: {error}");
            _output.WriteLine("\nPossible issues:");
            _output.WriteLine("1. Monday:ApiKey not set in configuration");
            _output.WriteLine("2. Invalid board ID");
            _output.WriteLine("3. API token doesn't have access to this board");
            return;
        }

        var items = await response.Content.ReadFromJsonAsync<List<MondayItemDto>>();

        if (items == null || !items.Any())
        {
            _output.WriteLine("Board has NO ITEMS");
            _output.WriteLine("\nTo use this connector, your board needs:");
            _output.WriteLine("- At least 1 group");
            _output.WriteLine("- At least 1 item in that group");
            return;
        }

        _output.WriteLine($"Found {items.Count} items\n");

        // Analyze each item
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            _output.WriteLine($"\n{'-'.ToString().PadRight(80, '-')}");
            _output.WriteLine($"ITEM #{i + 1}");
            _output.WriteLine($"{'-'.ToString().PadRight(80, '-')}");
            _output.WriteLine($"  ID:         {item.Id}");
            _output.WriteLine($"  Title:      {item.Title}");
            _output.WriteLine($"  Group ID:   {item.GroupId}");
            _output.WriteLine($"  Parent ID:  {item.ParentId ?? "(none - this is a top-level item)"}");
            _output.WriteLine($"  Created:    {item.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  Updated:    {item.UpdatedAt:yyyy-MM-dd HH:mm:ss}");

            if (item.ColumnValues.Any())
            {
                _output.WriteLine($"\n  COLUMNS ({item.ColumnValues.Count}):");
                foreach (var col in item.ColumnValues)
                {
                    var displayValue = !string.IsNullOrEmpty(col.Value.Text) ? col.Value.Text : col.Value.Value;
                    var value = displayValue ?? "(null)";
                    if (value.Length > 100)
                        value = value.Substring(0, 100) + "...";
                    _output.WriteLine($"    - {col.Key}: {value}");
                }
            }

            // Check for sub-items
            _output.WriteLine($"\n  Checking for sub-items...");
            var subItemsResponse = await Client.GetAsync($"/api/v1/items/{item.Id}/subitems");
            if (subItemsResponse.IsSuccessStatusCode)
            {
                var subItems = await subItemsResponse.Content.ReadFromJsonAsync<List<MondayItemDto>>();
                if (subItems?.Any() == true)
                {
                    _output.WriteLine($"  Found {subItems.Count} sub-items:");
                    foreach (var subItem in subItems)
                    {
                        _output.WriteLine($"    - ID: {subItem.Id}, Title: {subItem.Title}");
                    }
                }
                else
                {
                    _output.WriteLine($"  No sub-items");
                }
            }

            // Check for updates
            _output.WriteLine($"\n  Checking for updates/comments...");
            var updatesResponse = await Client.GetAsync($"/api/v1/items/{item.Id}/updates");
            if (updatesResponse.IsSuccessStatusCode)
            {
                var updates = await updatesResponse.Content.ReadFromJsonAsync<List<MondayUpdateDto>>();
                if (updates?.Any() == true)
                {
                    _output.WriteLine($"  Found {updates.Count} updates:");
                    foreach (var update in updates.Take(3))
                    {
                        var preview = update.BodyText.Length > 50
                            ? update.BodyText.Substring(0, 50) + "..."
                            : update.BodyText;
                        _output.WriteLine($"    - {update.CreatedAt:yyyy-MM-dd HH:mm}: {preview}");
                    }
                    if (updates.Count > 3)
                        _output.WriteLine($"    ... and {updates.Count - 3} more");
                }
                else
                {
                    _output.WriteLine($"  No updates/comments");
                }
            }
        }

        // Summary
        _output.WriteLine("\n" + "=".PadRight(80, '='));
        _output.WriteLine("SUMMARY & RECOMMENDATIONS");
        _output.WriteLine("=".PadRight(80, '='));

        var itemsWithSubItems = 0;
        var itemsWithUpdates = 0;

        foreach (var item in items)
        {
            var subItemsResp = await Client.GetAsync($"/api/v1/items/{item.Id}/subitems");
            if (subItemsResp.IsSuccessStatusCode)
            {
                var subItems = await subItemsResp.Content.ReadFromJsonAsync<List<MondayItemDto>>();
                if (subItems?.Any() == true) itemsWithSubItems++;
            }

            var updatesResp = await Client.GetAsync($"/api/v1/items/{item.Id}/updates");
            if (updatesResp.IsSuccessStatusCode)
            {
                var updates = await updatesResp.Content.ReadFromJsonAsync<List<MondayUpdateDto>>();
                if (updates?.Any() == true) itemsWithUpdates++;
            }
        }

        _output.WriteLine($"\nYour board is ready to use!");
        _output.WriteLine($"   - {items.Count} total items");
        _output.WriteLine($"   - {itemsWithSubItems} items have sub-items");
        _output.WriteLine($"   - {itemsWithUpdates} items have updates/comments");

        var groups = items.Select(i => i.GroupId).Distinct().Count();
        _output.WriteLine($"   - {groups} group(s)");

        if (items.Any() && items.First().ColumnValues.Any())
        {
            _output.WriteLine($"\nAvailable column IDs for write operations:");
            foreach (var col in items.First().ColumnValues.Keys.Take(10))
            {
                _output.WriteLine($"   - {col}");
            }
        }

        _output.WriteLine("\nNext steps:");
        _output.WriteLine("   1. Update testsettings.json StatusColumnId with one of the column IDs above");
        _output.WriteLine("   2. Update testsettings.json StatusLabel with a valid status option from your board");
        _output.WriteLine("   3. Remove [Fact(Skip = ...)] from tests you want to run");
    }

    [Fact]
    public async Task TestApiConnection_VerifyCredentials()
    {
        Config.Validate();

        _output.WriteLine("Testing Monday.com API connection...\n");

        var response = await Client.GetAsync($"/api/v1/boards/{Config.BoardId}/items");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Is Success: {response.IsSuccessStatusCode}\n");

        if (response.IsSuccessStatusCode)
        {
            _output.WriteLine("API connection successful!");
            _output.WriteLine("API token is valid");
            _output.WriteLine("Board is accessible");

            var items = await response.Content.ReadFromJsonAsync<List<MondayItemDto>>();
            _output.WriteLine($"Retrieved {items?.Count ?? 0} items");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine("Connection failed!");
            _output.WriteLine($"\nError response: {error}");
            _output.WriteLine("\nChecklist:");
            _output.WriteLine("[ ] Is Monday:ApiKey set in testsettings.json or MONDAY_API_TOKEN environment variable?");
            _output.WriteLine("[ ] Is the API token valid?");
            _output.WriteLine($"[ ] Does the token have access to board {Config.BoardId}?");
            _output.WriteLine("[ ] Is the board ID correct?");
        }
    }
}
