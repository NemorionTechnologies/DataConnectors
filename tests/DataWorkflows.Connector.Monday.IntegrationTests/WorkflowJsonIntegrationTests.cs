using System.Text.Json;
using DataWorkflows.Connector.Monday.Actions;
using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Application.Interfaces;
using DataWorkflows.Connector.Monday.Actions.Models;
using DataWorkflows.Engine.Core.Domain.Parsing;
using DataWorkflows.Contracts.Actions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataWorkflows.Connector.Monday.IntegrationTests;

public class EngineWorkflowIntegrationTests
{
    [Fact]
    public async Task WorkflowJson_ProducesExpectedMondayFilterDefinition()
    {
        var workflowPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "monday-filter-workflow.json");
        File.Exists(workflowPath).Should().BeTrue("workflow JSON fixture should be copied to the output directory");

        var parser = new WorkflowParser();
        var workflowJson = await File.ReadAllTextAsync(workflowPath);
        var workflow = parser.Parse(workflowJson);

        workflow.StartNode.Should().Be("fetch-items");
        var node = workflow.Nodes.Single(n => n.Id == "fetch-items");
        node.ActionType.Should().Be("monday.get-items");
        node.Parameters.Should().NotBeNull();

        var parametersDict = node.Parameters!
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value is JsonElement element ? (object?)element : kvp.Value);

        var returnedItems = new List<MondayItemDto>
        {
            new()
            {
                Id = "item-1",
                Title = "Sample Item",
                GroupId = "group-456",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                ColumnValues = new Dictionary<string, MondayColumnValueDto>
                {
                    ["status"] = new()
                    {
                        Id = "status",
                        Value = "{\"index\":1}",
                        Text = "Done"
                    }
                }
            }
        };

        MondayFilterDefinition? capturedFilter = null;

        var apiClientMock = new Mock<IMondayApiClient>(MockBehavior.Strict);
        apiClientMock
            .Setup(client => client.GetBoardItemsAsync(
                "board-123",
                It.IsAny<MondayFilterDefinition?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, MondayFilterDefinition?, CancellationToken>((_, filter, _) => capturedFilter = filter)
            .ReturnsAsync(returnedItems);

        var loggerMock = new Mock<ILogger<MondayGetItemsAction>>();
        var action = new MondayGetItemsAction(apiClientMock.Object, loggerMock.Object);

        var context = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: node.Id,
            Parameters: parametersDict,
            Services: new ServiceCollection().BuildServiceProvider());

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.Should().Be(ActionExecutionStatus.Succeeded);
        result.Outputs.Should().ContainKey("items");
        result.Outputs.Should().ContainKey("count");
        ((int)result.Outputs["count"]!).Should().Be(1);

        var itemsOutput = result.Outputs["items"].Should().BeOfType<List<MondayItem>>().Subject;
        itemsOutput.Should().HaveCount(1);
        itemsOutput[0].Id.Should().Be("item-1");

        capturedFilter.Should().NotBeNull();
        capturedFilter!.GroupId.Should().Be("group-456");
        capturedFilter.Rules.Should().HaveCount(1);
        var rule = capturedFilter.Rules[0];
        rule.ColumnId.Should().Be("status");
        rule.Operator.Should().Be("eq");
        rule.Value.Should().Be("Done");
        rule.ValueType.Should().Be("text");

        apiClientMock.Verify(client => client.GetBoardItemsAsync(
                "board-123",
                It.IsAny<MondayFilterDefinition?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}



