using DataWorkflows.Engine.Core.Domain.Validation;
using DataWorkflows.Engine.Core.Domain.Models;
using FluentAssertions;
using Xunit;

namespace DataWorkflows.Engine.Tests.Validation;

public class GraphValidatorTests
{
    private readonly GraphValidator _validator;

    public GraphValidatorTests()
    {
        _validator = new GraphValidator();
    }

    [Fact]
    public void Validate_ValidSimpleWorkflow_ShouldNotThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "test",
            DisplayName: "Test",
            StartNode: "step1",
            Nodes: new List<Node>
            {
                new("step1", "core.echo", new Dictionary<string, object> { ["message"] = "Hello" },
                    new List<Edge> { new("step2") }),
                new("step2", "core.echo", new Dictionary<string, object> { ["message"] = "World" })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ValidLinearChain_ShouldNotThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "linear",
            DisplayName: "Linear",
            StartNode: "step1",
            Nodes: new List<Node>
            {
                new("step1", "core.echo", new Dictionary<string, object> { ["message"] = "1" },
                    new List<Edge> { new("step2") }),
                new("step2", "core.echo", new Dictionary<string, object> { ["message"] = "2" },
                    new List<Edge> { new("step3") }),
                new("step3", "core.echo", new Dictionary<string, object> { ["message"] = "3" })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ValidParallelBranches_ShouldNotThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "parallel",
            DisplayName: "Parallel",
            StartNode: "start",
            Nodes: new List<Node>
            {
                new("start", "core.echo", new Dictionary<string, object> { ["message"] = "Start" },
                    new List<Edge> { new("branch-a"), new("branch-b") }),
                new("branch-a", "core.echo", new Dictionary<string, object> { ["message"] = "A" }),
                new("branch-b", "core.echo", new Dictionary<string, object> { ["message"] = "B" })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MissingStartNode_ShouldThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "invalid",
            DisplayName: "Invalid",
            StartNode: "nonexistent",
            Nodes: new List<Node>
            {
                new("step1", "core.echo", new Dictionary<string, object> { ["message"] = "Hello" })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*startNode*not found*");
    }

    [Fact]
    public void Validate_InvalidEdgeTarget_ShouldThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "invalid",
            DisplayName: "Invalid",
            StartNode: "step1",
            Nodes: new List<Node>
            {
                new("step1", "core.echo", new Dictionary<string, object> { ["message"] = "Hello" },
                    new List<Edge> { new("nonexistent") })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Edge target*not found*");
    }

    [Fact]
    public void Validate_SimpleCycle_ShouldThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "cycle",
            DisplayName: "Cycle",
            StartNode: "node-a",
            Nodes: new List<Node>
            {
                new("node-a", "core.echo", new Dictionary<string, object> { ["message"] = "A" },
                    new List<Edge> { new("node-b") }),
                new("node-b", "core.echo", new Dictionary<string, object> { ["message"] = "B" },
                    new List<Edge> { new("node-a") })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void Validate_ComplexCycle_ShouldThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "complex-cycle",
            DisplayName: "Complex Cycle",
            StartNode: "node-a",
            Nodes: new List<Node>
            {
                new("node-a", "core.echo", new Dictionary<string, object> { ["message"] = "A" },
                    new List<Edge> { new("node-b") }),
                new("node-b", "core.echo", new Dictionary<string, object> { ["message"] = "B" },
                    new List<Edge> { new("node-c") }),
                new("node-c", "core.echo", new Dictionary<string, object> { ["message"] = "C" },
                    new List<Edge> { new("node-d") }),
                new("node-d", "core.echo", new Dictionary<string, object> { ["message"] = "D" },
                    new List<Edge> { new("node-b") })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void Validate_SelfLoop_ShouldThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "self-loop",
            DisplayName: "Self Loop",
            StartNode: "node-a",
            Nodes: new List<Node>
            {
                new("node-a", "core.echo", new Dictionary<string, object> { ["message"] = "A" },
                    new List<Edge> { new("node-a") })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void Validate_EmptyNodes_ShouldThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "empty",
            DisplayName: "Empty",
            StartNode: "step1",
            Nodes: new List<Node>()
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_MultipleEdgesToSameTarget_ShouldNotThrow()
    {
        // Arrange
        var workflow = new WorkflowDefinition(
            Id: "multi-edge",
            DisplayName: "Multiple Edges",
            StartNode: "start",
            Nodes: new List<Node>
            {
                new("start", "core.echo", new Dictionary<string, object> { ["message"] = "Start" },
                    new List<Edge>
                    {
                        new("end", "success", "trigger.path === 'a'"),
                        new("end", "success", "trigger.path === 'b'")
                    }),
                new("end", "core.echo", new Dictionary<string, object> { ["message"] = "End" })
            }
        );

        // Act
        Action act = () => _validator.Validate(workflow);

        // Assert
        act.Should().NotThrow();
    }
}
