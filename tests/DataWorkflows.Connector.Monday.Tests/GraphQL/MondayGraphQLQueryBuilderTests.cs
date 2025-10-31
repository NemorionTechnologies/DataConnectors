using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Infrastructure.GraphQL;
using FluentAssertions;

namespace DataWorkflows.Connector.Monday.Tests.GraphQL;

public class MondayGraphQLQueryBuilderTests
{
    private readonly MondayGraphQLQueryBuilder _builder = new();

    [Fact]
    public void BuildGetBoardItemsQuery_IncludesGroupAndQueryParams()
    {
        var filter = new MondayFilterDefinition(
            GroupId: "group-1",
            Rules: Array.Empty<MondayFilterRule>(),
            CreatedAt: null,
            Condition: null);

        var queryParams = new MondayQueryParams(
            new[] { new MondayQueryRule("status", "eq", "Done", true) });

        var query = _builder.BuildGetBoardItemsQuery("123", filter, queryParams);

        query.Should().Contain("boards(ids: [123])");
        query.Should().Contain("groups: [\"group-1\"]");
        query.Should().Contain("query_params: { rules: [{ column_id: \"status\"");
        query.Should().Contain("compare_value: \"Done\"");
    }

    [Fact]
    public void BuildUpdateColumnValueMutation_EscapesValues()
    {
        var valueJson = "{\"text\":\"A\\B\"}";

        var mutation = _builder.BuildUpdateColumnValueMutation(
            "1",
            "2",
            "status\"column",
            valueJson);

        mutation.Should().Contain("column_id: \"status\\\"column\"");
        mutation.Should().Contain("value: \"{\\\"text\\\":\\\"A\\\\B\\\"}\"");
    }

    [Fact]
    public void BuildCreateItemMutation_AddsOptionalArguments()
    {
        var columnValues = new Dictionary<string, object>
        {
            ["status"] = new { text = "Done" }
        };

        var mutation = _builder.BuildCreateItemMutation(
            "10",
            "My Item",
            "group-123",
            columnValues);

        mutation.Should().Contain("item_name: \"My Item\"");
        mutation.Should().Contain("group_id: \"group-123\"");
        mutation.Should().Contain("column_values: \"{\\\"status\\\":{\\\"text\\\":\\\"Done\\\"}}\"");
    }

    [Fact]
    public void BuildGetBoardItemsQuery_WithNoFilters_OmitsOptionalArgs()
    {
        var query = _builder.BuildGetBoardItemsQuery(
            "42",
            MondayFilterDefinition.Empty,
            queryParams: null);

        query.Should().Contain("boards(ids: [42])");
        query.Should().NotContain("groups:");
        query.Should().NotContain("query_params:");
    }
}


