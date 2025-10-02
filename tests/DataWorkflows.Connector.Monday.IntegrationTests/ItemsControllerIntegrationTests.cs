using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

public class ItemsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ItemsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateItemColumnValue_ShouldReturn200_WithValidRequest()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        var expectedItem = new MondayItemDto
        {
            Id = "123",
            Title = "Updated Item",
            GroupId = "group1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ColumnValues = new Dictionary<string, MondayColumnValueDto>
            {
                { "status", new MondayColumnValueDto { Id = "status", Value = "{\"index\":1}", Text = "Done" } }
            }
        };

        mockApiClient
            .Setup(x => x.UpdateColumnValueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

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

        var request = new UpdateColumnValueRequest("456", "{ \"label\": \"Done\" }", ColumnId: null, ColumnTitle: null);
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PatchAsync("/api/v1/items/123/columns/status", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<MondayItemDto>();
        item.Should().NotBeNull();
        item!.Id.Should().Be("123");
        item.ColumnValues.Should().ContainKey("status");
    }

    [Fact]
    public async Task GetSubItems_ShouldReturn200_WithValidItemId()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        var expectedSubItems = new List<MondayItemDto>
        {
            new MondayItemDto
            {
                Id = "sub1",
                ParentId = "123",
                Title = "Sub Item",
                GroupId = "group1",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        mockApiClient
            .Setup(x => x.GetSubItemsAsync(
                It.IsAny<string>(),
                It.IsAny<GetItemsFilterModel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSubItems);

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
        var response = await client.GetAsync("/api/v1/items/123/subitems");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subItems = await response.Content.ReadFromJsonAsync<List<MondayItemDto>>();
        subItems.Should().NotBeNull();
        subItems.Should().HaveCount(1);
        subItems![0].ParentId.Should().Be("123");
    }

    [Fact]
    public async Task GetItemUpdates_ShouldReturn200_WithValidItemId()
    {
        // Arrange
        var mockApiClient = new Mock<IMondayApiClient>();
        var expectedUpdates = new List<MondayUpdateDto>
        {
            new MondayUpdateDto
            {
                Id = "update1",
                ItemId = "123",
                BodyText = "Test update",
                CreatorId = "user1",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        mockApiClient
            .Setup(x => x.GetItemUpdatesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUpdates);

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
        var response = await client.GetAsync("/api/v1/items/123/updates");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updates = await response.Content.ReadFromJsonAsync<List<MondayUpdateDto>>();
        updates.Should().NotBeNull();
        updates.Should().HaveCount(1);
        updates![0].ItemId.Should().Be("123");
    }
}
