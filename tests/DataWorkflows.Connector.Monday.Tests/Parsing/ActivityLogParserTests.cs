using DataWorkflows.Connector.Monday.Infrastructure.Parsing;
using FluentAssertions;

namespace DataWorkflows.Connector.Monday.Tests.Parsing;

public class ActivityLogParserTests
{
    private readonly ActivityLogParser _parser = new();

    [Fact]
    public void TryExtractActivityItemId_ReturnsRootItemId()
    {
        var data = "{\"item_id\":\"123\"}";

        var result = _parser.TryExtractActivityItemId(data);

        result.Should().Be("123");
    }

    [Fact]
    public void TryExtractActivityItemId_FallsBackToEntity()
    {
        var data = "{\"entity\":{\"item_id\":\"456\"}}";

        var result = _parser.TryExtractActivityItemId(data);

        result.Should().Be("456");
    }

    [Fact]
    public void TryExtractActivityItemId_ReturnsNullForInvalidJson()
    {
        var result = _parser.TryExtractActivityItemId("not-json");

        result.Should().BeNull();
    }

    [Fact]
    public void TryExtractActivityItemId_ReturnsNullWhenMissing()
    {
        var result = _parser.TryExtractActivityItemId("{\"data\":1}");

        result.Should().BeNull();
    }
}
