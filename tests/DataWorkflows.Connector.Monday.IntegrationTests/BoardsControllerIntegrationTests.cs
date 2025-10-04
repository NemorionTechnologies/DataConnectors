using System.Net;
using System.Net.Http.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

public class BoardsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BoardsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetItemsByBoardId_ShouldReturn200_WithValidBoardId()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        var expectedItems = new List<MondayItemDto>
        {
            new MondayItemDto
            {
                Id = "1",
                Title = "Test Item",
                GroupId = "group1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        mockApiClient
            .Setup(x => x.GetBoardItemsAsync(
                It.IsAny<string>(),
                It.IsAny<MondayFilterDefinition?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing IMondayApiClient registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMondayApiClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add mock IMondayApiClient
                services.AddSingleton(mockApiClient.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/boards/123/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<MondayItemDto>>();
        items.Should().NotBeNull();
        items.Should().HaveCount(1);
        items![0].Title.Should().Be("Test Item");
    }

    [Fact]
    public async Task GetBoardActivity_ShouldReturn200_WithValidBoardId()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        var expectedLogs = new List<MondayActivityLogDto>
        {
            new MondayActivityLogDto
            {
                EventType = "create_item",
                UserId = "user1",
                CreatedAt = DateTimeOffset.UtcNow,
                EventDataJson = "{}"
            }
        };

        mockApiClient
            .Setup(x => x.GetBoardActivityAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLogs);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMondayApiClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton(mockApiClient.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/boards/123/activity");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<List<MondayActivityLogDto>>();
        logs.Should().NotBeNull();
        logs.Should().HaveCount(1);
        logs![0].EventType.Should().Be("create_item");
    }

    [Fact]
    public async Task CorrelationId_ShouldBeAddedToResponse_WhenNotProvided()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        mockApiClient
            .Setup(x => x.GetBoardItemsAsync(
                It.IsAny<string>(),
                It.IsAny<MondayFilterDefinition?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MondayItemDto>());

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMondayApiClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton(mockApiClient.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/boards/123/items");

        // Assert
        response.Headers.Should().ContainKey("X-Correlation-ID");
        var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CorrelationId_ShouldUseProvidedValue_WhenHeaderPresent()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        mockApiClient
            .Setup(x => x.GetBoardItemsAsync(
                It.IsAny<string>(),
                It.IsAny<MondayFilterDefinition?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MondayItemDto>());

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMondayApiClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton(mockApiClient.Object);
            });
        }).CreateClient();

        var expectedCorrelationId = "test-correlation-id-123";
        client.DefaultRequestHeaders.Add("X-Correlation-ID", expectedCorrelationId);

        // Act
        var response = await client.GetAsync("/api/v1/boards/123/items");

        // Assert
        response.Headers.Should().ContainKey("X-Correlation-ID");
        var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
        correlationId.Should().Be(expectedCorrelationId);
    }
}

