using DataWorkflows.Engine.Core.Domain.Parsing;
using DataWorkflows.Engine.Core.Domain.Validation;
using FluentAssertions;
using Xunit;

namespace DataWorkflows.Engine.Tests.Fixtures;

public class FixtureValidationTests
{
    private readonly WorkflowParser _parser;
    private readonly GraphValidator _validator;

    public FixtureValidationTests()
    {
        _parser = new WorkflowParser();
        _validator = new GraphValidator();
    }

    [Theory]
    [InlineData("Fixtures/Valid/simple-linear.json")]
    [InlineData("Fixtures/Valid/parallel-fanout-fanin.json")]
    [InlineData("Fixtures/Valid/conditional-branching.json")]
    [InlineData("Fixtures/Valid/template-trigger-context.json")]
    [InlineData("Fixtures/Valid/first-match-routing.json")]
    [InlineData("Fixtures/Valid/rerender-on-retry.json")]
    [InlineData("Fixtures/Valid/failure-edge-handling.json")]
    public void ValidFixtures_ShouldParseAndValidate(string fixturePath)
    {
        // Arrange
        var json = File.ReadAllText(fixturePath);

        // Act
        var workflow = _parser.Parse(json);
        Action act = () => _validator.Validate(workflow);

        // Assert
        workflow.Should().NotBeNull();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Fixtures/EdgeCases/single-node-no-edges.json")]
    [InlineData("Fixtures/EdgeCases/empty-parameters.json")]
    [InlineData("Fixtures/EdgeCases/null-parameters.json")]
    [InlineData("Fixtures/EdgeCases/deep-nesting.json")]
    public void EdgeCaseFixtures_ShouldParseAndValidate(string fixturePath)
    {
        // Arrange
        var json = File.ReadAllText(fixturePath);

        // Act
        var workflow = _parser.Parse(json);
        Action act = () => _validator.Validate(workflow);

        // Assert
        workflow.Should().NotBeNull();
        act.Should().NotThrow();
    }

    [Fact]
    public void InvalidFixture_MissingStartNode_ShouldValidationFail()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Invalid/missing-start-node.json");

        // Act
        var workflow = _parser.Parse(json);
        Action act = () => _validator.Validate(workflow);

        // Assert
        workflow.Should().NotBeNull();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*startNode*not found*");
    }

    [Fact]
    public void InvalidFixture_CyclicGraph_ShouldValidationFail()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Invalid/cyclic-graph.json");

        // Act
        var workflow = _parser.Parse(json);
        Action act = () => _validator.Validate(workflow);

        // Assert
        workflow.Should().NotBeNull();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void InvalidFixture_InvalidEdgeTarget_ShouldValidationFail()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Invalid/invalid-edge-target.json");

        // Act
        var workflow = _parser.Parse(json);
        Action act = () => _validator.Validate(workflow);

        // Assert
        workflow.Should().NotBeNull();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Edge target*not found*");
    }

    [Fact]
    public void InvalidFixture_EmptyNodes_ShouldValidationFail()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Invalid/empty-nodes.json");

        // Act
        var workflow = _parser.Parse(json);
        Action act = () => _validator.Validate(workflow);

        // Assert
        workflow.Should().NotBeNull();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SimpleLinear_ShouldHaveCorrectStructure()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/simple-linear.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Id.Should().Be("simple-linear");
        workflow.StartNode.Should().Be("step1");
        workflow.Nodes.Should().HaveCount(2);

        var step1 = workflow.Nodes.First(n => n.Id == "step1");
        step1.ActionType.Should().Be("core.echo");
        step1.Edges.Should().HaveCount(1);
        step1.Edges![0].TargetNode.Should().Be("step2");
        step1.Edges[0].When.Should().Be("success");
    }

    [Fact]
    public void ParallelFanoutFanin_ShouldHaveCorrectStructure()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/parallel-fanout-fanin.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        workflow.Nodes.Should().HaveCount(5);

        var start = workflow.Nodes.First(n => n.Id == "start");
        start.Edges.Should().HaveCount(3);
        start.Edges!.Select(e => e.TargetNode).Should().BeEquivalentTo(new[] { "parallel-a", "parallel-b", "parallel-c" });

        var join = workflow.Nodes.First(n => n.Id == "join");
        join.Edges.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ConditionalBranching_ShouldHaveCorrectConditions()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/conditional-branching.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var checkNode = workflow.Nodes.First(n => n.Id == "check-status");
        checkNode.Edges.Should().HaveCount(3);

        var approvedEdge = checkNode.Edges!.First(e => e.TargetNode == "approved-path");
        approvedEdge.Condition.Should().Contain("approved");

        var rejectedEdge = checkNode.Edges!.First(e => e.TargetNode == "rejected-path");
        rejectedEdge.Condition.Should().Contain("rejected");
    }

    [Fact]
    public void FirstMatchRouting_ShouldHaveCorrectPolicy()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/first-match-routing.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var checkNode = workflow.Nodes.First(n => n.Id == "check-priority");
        checkNode.RoutePolicy.Should().Be("firstMatch");
        checkNode.Edges.Should().HaveCount(3);
    }

    [Fact]
    public void RerenderOnRetry_ShouldHaveCorrectPolicy()
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
    public void FailureEdgeHandling_ShouldHaveAllEdgeTypes()
    {
        // Arrange
        var json = File.ReadAllText("Fixtures/Valid/failure-edge-handling.json");

        // Act
        var workflow = _parser.Parse(json);

        // Assert
        var riskyNode = workflow.Nodes.First(n => n.Id == "risky-operation");
        riskyNode.Edges.Should().HaveCount(3);

        var edgeTypes = riskyNode.Edges!.Select(e => e.When).ToList();
        edgeTypes.Should().Contain("success");
        edgeTypes.Should().Contain("failure");
        edgeTypes.Should().Contain("always");
    }
}
