using Microsoft.Extensions.Configuration;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

public class TestConfiguration
{
    public string BoardId { get; set; } = string.Empty;
    public string StatusColumnTitle { get; set; } = "Status";
    public string StatusLabel { get; set; } = "Working on it";
    public string TimelineColumnTitle { get; set; } = string.Empty;
    public string LinkColumnTitle { get; set; } = "Link";
    public string TestItemId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    public static TestConfiguration Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("testsettings.Development.json", optional: true)
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var config = new TestConfiguration();
        configuration.GetSection("Monday").Bind(config);

        // Expand environment variable placeholders
        config.BoardId = ExpandEnvironmentVariables(config.BoardId);
        config.StatusColumnTitle = ExpandEnvironmentVariables(config.StatusColumnTitle);
        config.StatusLabel = ExpandEnvironmentVariables(config.StatusLabel);
        config.TimelineColumnTitle = ExpandEnvironmentVariables(config.TimelineColumnTitle);
        config.LinkColumnTitle = ExpandEnvironmentVariables(config.LinkColumnTitle);
        config.TestItemId = ExpandEnvironmentVariables(config.TestItemId);
        config.ApiKey = ExpandEnvironmentVariables(config.ApiKey);

        return config;
    }

    private static string ExpandEnvironmentVariables(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Replace ${VAR_NAME} with environment variable value
        var pattern = @"\$\{([^}]+)\}";
        return System.Text.RegularExpressions.Regex.Replace(value, pattern, match =>
        {
            var varName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(varName) ?? match.Value;
        });
    }

    public void Validate()
    {
        if (string.IsNullOrEmpty(BoardId) || BoardId == "YOUR_BOARD_ID_HERE")
        {
            throw new InvalidOperationException(
                "Monday.BoardId is not configured. Please update testsettings.json or set MONDAY_BOARD_ID environment variable.");
        }

        if (string.IsNullOrEmpty(ApiKey))
        {
            throw new InvalidOperationException(
                "Monday.ApiKey is not configured. Please update testsettings.json or set MONDAY_API_TOKEN environment variable.");
        }
    }
}
