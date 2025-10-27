using DataWorkflows.Engine.Core.Domain.Parsing;
using DataWorkflows.Engine.Core.Domain.Models;
using FluentAssertions;
using Xunit;

namespace DataWorkflows.Engine.Tests.Parsing;

public class WorkflowParserTests
{
    private readonly WorkflowParser _parser;

    public WorkflowParserTests()
    {
        _parser = new WorkflowParser();
    }

    [Theory]
    [InlineData("Fixtures/Valid/simple-linear.json", "simple-linear", 2)]
    [InlineData("Fixtures/Valid/parallel-fanout-fanin.json", "parallel-fanout-fanin", 5)]
    [InlineData("Fixtures/Valid/conditional-branching.json", "conditional-branching", 4)]
    [InlineData("Fixtures/Valid/template-trigger-context.json", "template-trigger-context", 3)]
    [InlineData("Fixtures/Valid/first-match-routing.json", "first-match-routing", 4)]
    [InlineData("Fixtures/Valid/rerender-on-retry.json", "rerender-on-retry", 1)]
    [InlineData("Fixtures/Valid/failure-edge-handling.json", "failure-edge-handling", 4)]
    public void Parse_ValidWorkflows_ShouldSucceed(string fixturePath, string expectedId, int expectedNodeCount)
    {
        // Arrange
        var json = File.ReadAllText(fixturePath);

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Id.Should().Be(expectedId);
        workflow.Nodes.Should().HaveCount(expectedNodeCount);
    }

    [Theory]
    [InlineData("Fixtures/EdgeCases/single-node-no-edges.json")]
    [InlineData("Fixtures/EdgeCases/empty-parameters.json")]
    [InlineData("Fixtures/EdgeCases/null-parameters.json")]
    [InlineData("Fixtures/EdgeCases/deep-nesting.json")]
    public void Parse_EdgeCaseWorkflows_ShouldSucceed(string fixturePath)
    {
        // Arrange
        var json = File.ReadAllText(fixturePath);

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Nodes.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_ParallelFanout_ShouldHaveThreeBranches()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/parallel-fanout-fanin.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var startNode = workflow.Nodes.First(n => n.Id == "start");
        startNode.Edges.Should().HaveCount(3);
        startNode.Edges!.Select(e => e.TargetNode).Should().Contain(new[] { "parallel-a", "parallel-b", "parallel-c" });
    }

    [Fact]
    public void Parse_ConditionalBranching_ShouldHaveConditions()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/conditional-branching.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var checkNode = workflow.Nodes.First(n => n.Id == "check-status");
        checkNode.Edges.Should().HaveCount(3);
        checkNode.Edges!.All(e => !string.IsNullOrEmpty(e.Condition)).Should().BeTrue();
    }

    [Fact]
    public void Parse_TemplatingWorkflow_ShouldContainTemplateVariables()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/template-trigger-context.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var greetNode = workflow.Nodes.First(n => n.Id == "greet-user");
        greetNode.Parameters.Should().ContainKey("message");
        greetNode.Parameters!["message"].ToString().Should().Contain("{{ trigger.username }}");
    }

    [Fact]
    public void Parse_FirstMatchRouting_ShouldHaveCorrectPolicy()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/first-match-routing.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var checkNode = workflow.Nodes.First(n => n.Id == "check-priority");
        checkNode.RoutePolicy.Should().Be("firstMatch");
    }

    [Fact]
    public void Parse_RerenderOnRetry_ShouldHaveCorrectPolicy()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/rerender-on-retry.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var node = workflow.Nodes.First();
        node.Policies.Should().NotBeNull();
        node.Policies!.RerenderOnRetry.Should().BeTrue();
    }

    [Fact]
    public void Parse_SingleNodeNoEdges_ShouldHaveNoEdges()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/EdgeCases/single-node-no-edges.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Nodes.Should().HaveCount(1);
        workflow.Nodes.First().Edges.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Parse_EmptyParameters_ShouldHaveEmptyParametersObject()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/EdgeCases/empty-parameters.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Nodes.First().Parameters.Should().NotBeNull();
        workflow.Nodes.First().Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullParameters_ShouldHaveNullParameters()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/EdgeCases/null-parameters.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Nodes.First().Parameters.Should().BeNull();
    }

    [Fact]
    public void Parse_DeepNesting_ShouldPreserveNestedStructure()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/EdgeCases/deep-nesting.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var node = workflow.Nodes.First();
        node.Parameters.Should().ContainKey("nested");
    }

    [Fact]
    public void Parse_InvalidJson_ShouldThrow()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act
        Action act = () => _parser.Parse(json);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_EmptyString_ShouldThrow()
    {
        // Arrange
        var json = "";

        // Act
        Action act = () => _parser.Parse(json);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_NullString_ShouldThrow()
    {
        // Arrange
        string json = null!;

        // Act
        Action act = () => _parser.Parse(json);

        // Assert
        act.Should().Throw<Exception>();
    }
}
