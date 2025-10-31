using System.Net;
using System.Text;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Domain.Exceptions;
using DataWorkflows.Connector.Monday.Infrastructure.GraphQL;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataWorkflows.Connector.Monday.Tests.GraphQL;

public class MondayGraphQLExecutorTests
{
    private readonly Mock<ILogger> _loggerMock = new();

    [Fact]
    public async Task ExecuteQueryAsync_ReturnsDeserializedPayload()
    {
        var query = "{ boards { id } }";
        string? capturedBody = null;

        var handler = new FakeHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();

            var responsePayload = new TestResponse { Data = new TestData { Value = 42 } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(responsePayload),
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        };
        var executor = new MondayGraphQLExecutor(client, _loggerMock.Object);

        var result = await executor.ExecuteQueryAsync<TestResponse>(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Value.Should().Be(42);
        capturedBody.Should().Contain(query);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowsResourceNotFoundForGraphQlNotFoundMessage()
    {
        var handler = CreateErrorHandler("Board not found");
        var executor = new MondayGraphQLExecutor(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") }, _loggerMock.Object);

        var act = async () => await executor.ExecuteQueryAsync<TestResponse>("query", CancellationToken.None);

        await act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowsInvalidOperationForGraphQlError()
    {
        var handler = CreateErrorHandler("Something went wrong");
        var executor = new MondayGraphQLExecutor(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") }, _loggerMock.Object);

        var act = async () => await executor.ExecuteQueryAsync<TestResponse>("query", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Something went wrong*");
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowsForHttpFailure()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        }));

        var executor = new MondayGraphQLExecutor(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") }, _loggerMock.Object);

        var act = async () => await executor.ExecuteQueryAsync<TestResponse>("query", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static FakeHttpMessageHandler CreateErrorHandler(string message) =>
        new((_, _) =>
        {
            var response = new
            {
                errors = new[]
                {
                    new { message }
                }
            };

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(response),
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(httpResponse);
        });

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _handler(request, cancellationToken);
        }
    }

    private sealed class TestResponse
    {
        public TestData? Data { get; set; }
    }

    private sealed class TestData
    {
        public int Value { get; set; }
    }
}
