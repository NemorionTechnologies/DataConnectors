using DataWorkflows.Connector.Monday.Application.Commands.UpdateColumnValue;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataWorkflows.Connector.Monday.Tests.Handlers;

public class UpdateColumnValueCommandHandlerTests
{
    private readonly Mock<IMondayApiClient> _mockApiClient;
    private readonly Mock<IColumnResolverService> _mockColumnResolver;
    private readonly Mock<ILogger<UpdateColumnValueCommandHandler>> _mockLogger;
    private readonly UpdateColumnValueCommandHandler _handler;

    public UpdateColumnValueCommandHandlerTests()
    {
        _mockApiClient = new Mock<IMondayApiClient>();
        _mockColumnResolver = new Mock<IColumnResolverService>();
        _mockLogger = new Mock<ILogger<UpdateColumnValueCommandHandler>>();
        _handler = new UpdateColumnValueCommandHandler(
            _mockApiClient.Object,
            _mockColumnResolver.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnUpdatedItem_WhenUpdateSucceeds()
    {
        // Arrange
        var boardId = "456";
        var itemId = "123";
        var columnId = "status";
        var valueJson = "{ \"label\": \"Done\" }";
        var expectedItem = new MondayItemDto
        {
            Id = itemId,
            Title = "Test Item",
            GroupId = "group1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ColumnValues = new Dictionary<string, MondayColumnValueDto>
            {
                { columnId, new MondayColumnValueDto { Id = columnId, Value = "{\"index\":1}", Text = "Done" } }
            }
        };

        _mockColumnResolver
            .Setup(x => x.ResolveColumnIdAsync(boardId, columnId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(columnId);

        _mockApiClient
            .Setup(x => x.UpdateColumnValueAsync(boardId, itemId, columnId, valueJson, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        var command = new UpdateColumnValueCommand(boardId, itemId, valueJson, ColumnId: columnId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedItem);
        _mockColumnResolver.Verify(
            x => x.ResolveColumnIdAsync(boardId, columnId, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockApiClient.Verify(
            x => x.UpdateColumnValueAsync(boardId, itemId, columnId, valueJson, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("{ \"label\": \"Done\" }", "status")]
    [InlineData("{ \"url\": \"https://example.com\", \"text\": \"Link\" }", "link")]
    [InlineData("{ \"from\": \"2025-10-26\", \"to\": \"2025-10-28\" }", "timeline")]
    public async Task Handle_ShouldHandleDifferentColumnTypes(string valueJson, string columnId)
    {
        // Arrange
        var boardId = "456";
        var itemId = "123";
        var expectedItem = new MondayItemDto
        {
            Id = itemId,
            Title = "Test Item",
            GroupId = "group1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockColumnResolver
            .Setup(x => x.ResolveColumnIdAsync(boardId, columnId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(columnId);

        _mockApiClient
            .Setup(x => x.UpdateColumnValueAsync(boardId, itemId, columnId, valueJson, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        var command = new UpdateColumnValueCommand(boardId, itemId, valueJson, ColumnId: columnId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(itemId);
    }

    [Fact]
    public async Task Handle_ShouldResolveColumnTitle_WhenColumnTitleProvided()
    {
        // Arrange
        var boardId = "456";
        var itemId = "123";
        var columnTitle = "Status";
        var resolvedColumnId = "status";
        var valueJson = "{ \"label\": \"Done\" }";
        var expectedItem = new MondayItemDto
        {
            Id = itemId,
            Title = "Test Item",
            GroupId = "group1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockColumnResolver
            .Setup(x => x.ResolveColumnIdAsync(boardId, null, columnTitle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedColumnId);

        _mockApiClient
            .Setup(x => x.UpdateColumnValueAsync(boardId, itemId, resolvedColumnId, valueJson, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        var command = new UpdateColumnValueCommand(boardId, itemId, valueJson, ColumnTitle: columnTitle);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(itemId);
        _mockColumnResolver.Verify(
            x => x.ResolveColumnIdAsync(boardId, null, columnTitle, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockApiClient.Verify(
            x => x.UpdateColumnValueAsync(boardId, itemId, resolvedColumnId, valueJson, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
