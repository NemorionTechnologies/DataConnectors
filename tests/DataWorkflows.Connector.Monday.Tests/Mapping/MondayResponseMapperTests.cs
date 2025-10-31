using System.Text.Json;
using DataWorkflows.Connector.Monday.Infrastructure.Mapping;
using DataWorkflows.Connector.Monday.Infrastructure.Parsing;
using FluentAssertions;

namespace DataWorkflows.Connector.Monday.Tests.Mapping;

public class MondayResponseMapperTests
{
    private readonly MondayResponseMapper _mapper = new(new ActivityLogParser());

    [Fact]
    public void MapToMondayItemDto_MapsExpectedFields()
    {
        var json = JsonDocument.Parse("""
{
  "id": "123",
  "name": "Sample",
  "group": { "id": "group-1" },
  "created_at": "2024-01-01T10:00:00Z",
  "updated_at": "2024-01-02T11:00:00Z",
  "parent_item": { "id": "456" },
  "column_values": [
    { "id": "status", "value": "\"1\"", "text": "Done" }
  ]
}
""").RootElement;

        var result = _mapper.MapToMondayItemDto(json);

        result.Id.Should().Be("123");
        result.ParentId.Should().Be("456");
        result.Title.Should().Be("Sample");
        result.GroupId.Should().Be("group-1");
        result.CreatedAt.Should().Be(DateTimeOffset.Parse("2024-01-01T10:00:00Z"));
        result.UpdatedAt.Should().Be(DateTimeOffset.Parse("2024-01-02T11:00:00Z"));
        result.ColumnValues.Should().ContainKey("status");
        result.ColumnValues["status"].Value.Should().Be("\"1\"");
        result.ColumnValues["status"].Text.Should().Be("Done");
    }

    [Fact]
    public void MapToMondayItemDto_DefaultsWhenFieldsMissing()
    {
        var json = JsonDocument.Parse("""
{
  "id": "123"
}
""").RootElement;

        var result = _mapper.MapToMondayItemDto(json);

        result.ParentId.Should().BeNull();
        result.Title.Should().BeEmpty();
        result.GroupId.Should().BeEmpty();
        result.ColumnValues.Should().BeEmpty();
    }

    [Fact]
    public void MapToMondayActivityLogDto_UsesParserForItemId()
    {
        var log = JsonDocument.Parse("""
{
  "event": "create_item",
  "user_id": "user-1",
  "created_at": "2024-01-05T08:30:00Z",
  "data": "{\"item_id\":\"123\"}"
}
""").RootElement;

        var result = _mapper.MapToMondayActivityLogDto(log);

        result.EventType.Should().Be("create_item");
        result.UserId.Should().Be("user-1");
        result.ItemId.Should().Be("123");
        result.EventDataJson.Should().Contain("item_id");
        result.CreatedAt.Should().Be(DateTimeOffset.Parse("2024-01-05T08:30:00Z"));
    }

    [Fact]
    public void MapToMondayUpdateDto_MapsFields()
    {
        var update = JsonDocument.Parse("""
{
  "id": "u1",
  "body": "Hello",
  "creator_id": "user-2",
  "created_at": "2024-01-07T09:45:00Z"
}
""").RootElement;

        var result = _mapper.MapToMondayUpdateDto(update, "123");

        result.Id.Should().Be("u1");
        result.ItemId.Should().Be("123");
        result.BodyText.Should().Be("Hello");
        result.CreatorId.Should().Be("user-2");
        result.CreatedAt.Should().Be(DateTimeOffset.Parse("2024-01-07T09:45:00Z"));
    }

    [Fact]
    public void MapToColumnMetadata_MapsProperties()
    {
        var column = JsonDocument.Parse("""
{
  "id": "col1",
  "title": "Status",
  "type": "color"
}
""").RootElement;

        var result = _mapper.MapToColumnMetadata(column);

        result.Id.Should().Be("col1");
        result.Title.Should().Be("Status");
        result.Type.Should().Be("color");
    }
}
