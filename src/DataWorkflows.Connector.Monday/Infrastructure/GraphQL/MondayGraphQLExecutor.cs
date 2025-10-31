using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataWorkflows.Connector.Monday.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL;

internal class MondayGraphQLExecutor
{
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public MondayGraphQLExecutor(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<T?> ExecuteQueryAsync<T>(string query, CancellationToken cancellationToken)
    {
        var request = new { query };
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Executing GraphQL query: {Query}", query);

        var httpResponse = await _httpClient.PostAsync(string.Empty, content, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("GraphQL response: {Response}", responseBody);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError(
                "GraphQL query failed with status {StatusCode}: {Response}",
                httpResponse.StatusCode,
                responseBody);

            httpResponse.EnsureSuccessStatusCode();
        }

        using (var document = JsonDocument.Parse(responseBody))
        {
            LogGraphQlComplexity(document);

            if (document.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Array &&
                errorsElement.GetArrayLength() > 0)
            {
                var firstError = errorsElement[0];
                var message = firstError.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? "GraphQL error"
                    : "GraphQL error";

                if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ResourceNotFoundException(message);
                }

                throw new InvalidOperationException($"GraphQL error: {message}");
            }
        }

        return JsonSerializer.Deserialize<T>(responseBody, ResponseSerializerOptions);
    }

    private void LogGraphQlComplexity(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("extensions", out var extensions) ||
            extensions.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!extensions.TryGetProperty("complexity", out var complexity) ||
            complexity.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var total = TryGetDouble(complexity, "total");
        var remaining = TryGetDouble(complexity, "remaining");
        var queryCost = TryGetDouble(complexity, "query");
        var after = TryGetDouble(complexity, "after");

        if (total.HasValue || remaining.HasValue || queryCost.HasValue || after.HasValue)
        {
            _logger.LogDebug(
                "GraphQL complexity: total={Total}, remaining={Remaining}, query={QueryCost}, after={After}",
                total,
                remaining,
                queryCost,
                after);
        }
        else
        {
            _logger.LogDebug("GraphQL complexity payload: {Payload}", complexity.GetRawText());
        }
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.GetDouble(),
            JsonValueKind.String when double.TryParse(
                property.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) => value,
            _ => null
        };
    }
}
