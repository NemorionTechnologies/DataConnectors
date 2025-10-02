using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Application.Queries.GetBoardItems;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataWorkflows.Connector.Monday.Tests.Handlers;

public class GetBoardItemsQueryHandlerTests
{
    private readonly Mock<IMondayApiClient> _mockApiClient;
    private readonly Mock<ILogger<GetBoardItemsQueryHandler>> _mockLogger;
    private readonly GetBoardItemsQueryHandler _handler;

    public GetBoardItemsQueryHandlerTests()
    {
        _mockApiClient = new Mock<IMondayApiClient>();
        _mockLogger = new Mock<ILogger<GetBoardItemsQueryHandler>>();
        _handler = new GetBoardItemsQueryHandler(_mockApiClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnItems_WhenBoardExists()
    {
        // Arrange
        var boardId = "123";
        var filter = new GetItemsFilterModel();
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

        _mockApiClient
            .Setup(x => x.GetBoardItemsAsync(boardId, filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        var query = new GetBoardItemsQuery(boardId, filter);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedItems);
        _mockApiClient.Verify(x => x.GetBoardItemsAsync(boardId, filter, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoItemsExist()
    {
        // Arrange
        var boardId = "123";
        var filter = new GetItemsFilterModel();
        var expectedItems = new List<MondayItemDto>();

        _mockApiClient
            .Setup(x => x.GetBoardItemsAsync(boardId, filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        var query = new GetBoardItemsQuery(boardId, filter);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
