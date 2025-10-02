using System.Net;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace DataWorkflows.Connector.Monday.Presentation.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message) = exception switch
        {
            ResourceNotFoundException notFoundEx => (HttpStatusCode.NotFound, notFoundEx.Message),
            ArgumentException argEx => (HttpStatusCode.BadRequest, argEx.Message),
            InvalidOperationException invalidOpEx => (HttpStatusCode.BadRequest, invalidOpEx.Message),
            HttpRequestException httpEx => (HttpStatusCode.BadGateway, "External API error: " + httpEx.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        _logger.LogError(
            exception,
            "Exception occurred: {Message}, StatusCode: {StatusCode}",
            exception.Message,
            statusCode);

        var response = new
        {
            status = (int)statusCode,
            message = message,
            timestamp = DateTime.UtcNow
        };

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response),
            cancellationToken);

        return true;
    }
}
