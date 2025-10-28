using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SeedWorkflows;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static string? baseUrl;

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Workflow Seeder - Load fixture workflows into database");
        Console.WriteLine("========================================================\n");

        // Parse arguments
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: SeedWorkflows <path-to-workflow.json> [api-base-url]");
            Console.WriteLine("  path-to-workflow.json: Path to a single workflow JSON file or directory containing workflows");
            Console.WriteLine("  api-base-url: Optional API base URL (default: http://localhost:5000)");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  SeedWorkflows fixtures/bundle1/simple-echo-workflow.json");
            Console.WriteLine("  SeedWorkflows fixtures/bundle1");
            Console.WriteLine("  SeedWorkflows fixtures http://localhost:8080");
            return 1;
        }

        var path = args[0];
        baseUrl = args.Length > 1 ? args[1] : "http://localhost:5000";

        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        Console.WriteLine($"API Base URL: {baseUrl}\n");

        try
        {
            // Check if path is a file or directory
            if (File.Exists(path))
            {
                await SeedWorkflow(path);
            }
            else if (Directory.Exists(path))
            {
                await SeedDirectory(path);
            }
            else
            {
                Console.WriteLine($"ERROR: Path not found: {path}");
                return 1;
            }

            Console.WriteLine("\n========================================================");
            Console.WriteLine("Seeding completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    static async Task SeedDirectory(string dirPath)
    {
        Console.WriteLine($"Scanning directory: {dirPath}\n");

        var jsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.AllDirectories);
        var workflowFiles = new List<string>();

        foreach (var file in jsonFiles)
        {
            // Skip execute-request.json files
            if (Path.GetFileName(file).Equals("execute-request.json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  Skipping: {file} (execute-request)");
                continue;
            }

            workflowFiles.Add(file);
        }

        Console.WriteLine($"Found {workflowFiles.Count} workflow file(s)\n");

        foreach (var file in workflowFiles)
        {
            await SeedWorkflow(file);
        }
    }

    static async Task SeedWorkflow(string filePath)
    {
        Console.WriteLine($"Processing: {filePath}");

        var json = await File.ReadAllTextAsync(filePath);

        // Parse to validate and extract workflow ID
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  ERROR: Invalid JSON - {ex.Message}");
            throw;
        }

        if (!doc.RootElement.TryGetProperty("id", out var idElement))
        {
            Console.WriteLine($"  ERROR: Workflow JSON missing 'id' property");
            throw new InvalidOperationException("Workflow must have an 'id' property");
        }

        var workflowId = idElement.GetString();
        var displayName = doc.RootElement.TryGetProperty("displayName", out var nameElement)
            ? nameElement.GetString()
            : workflowId;

        Console.WriteLine($"  Workflow ID: {workflowId}");
        Console.WriteLine($"  Display Name: {displayName}");

        // Step 1: Create/Update Draft
        Console.Write("  [1/2] Creating draft... ");
        var createRequest = new
        {
            definitionJson = json,
            description = $"Seeded from {Path.GetFileName(filePath)}"
        };

        var createResponse = await httpClient.PostAsJsonAsync("/api/v1/workflows", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();

        if (!createResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("FAILED");
            Console.WriteLine($"  Status: {createResponse.StatusCode}");
            Console.WriteLine($"  Response: {createContent}");
            throw new HttpRequestException($"Failed to create draft workflow: {createResponse.StatusCode}");
        }

        var createResult = JsonSerializer.Deserialize<JsonElement>(createContent);
        var status = createResult.GetProperty("status").GetString();
        Console.WriteLine($"OK ({status})");

        // Step 2: Publish
        Console.Write("  [2/2] Publishing... ");
        var publishResponse = await httpClient.PostAsync($"/api/v1/workflows/{workflowId}/publish?autoActivate=true", null);
        var publishContent = await publishResponse.Content.ReadAsStringAsync();

        if (!publishResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("FAILED");
            Console.WriteLine($"  Status: {publishResponse.StatusCode}");
            Console.WriteLine($"  Response: {publishContent}");
            throw new HttpRequestException($"Failed to publish workflow: {publishResponse.StatusCode}");
        }

        var publishResult = JsonSerializer.Deserialize<JsonElement>(publishContent);
        var version = publishResult.GetProperty("version").GetInt32();
        var publishStatus = publishResult.GetProperty("status").GetString();
        var created = publishResult.GetProperty("created").GetBoolean();
        var message = publishResult.GetProperty("message").GetString();

        Console.WriteLine($"OK (v{version}, {publishStatus})");
        Console.WriteLine($"  {message}");

        // Show warnings if present
        if (publishResult.TryGetProperty("warnings", out var warningsElement) &&
            warningsElement.ValueKind == JsonValueKind.Array &&
            warningsElement.GetArrayLength() > 0)
        {
            Console.WriteLine("  Warnings:");
            foreach (var warning in warningsElement.EnumerateArray())
            {
                Console.WriteLine($"    - {warning.GetString()}");
            }
        }

        Console.WriteLine();
    }
}
