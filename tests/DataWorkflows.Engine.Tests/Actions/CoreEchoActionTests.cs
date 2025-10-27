using DataWorkflows.Contracts.Actions;
using DataWorkflows.Engine.Infrastructure.Actions;
using FluentAssertions;
using Xunit;

namespace DataWorkflows.Engine.Tests.Actions;

public class CoreEchoActionTests
{
    private readonly CoreEchoAction _action;

    public CoreEchoActionTests()
    {
        _action = new CoreEchoAction();
    }

    [Fact]
    public void Type_ShouldReturnCoreEcho()
    {
        // Act
        var type = _action.Type;

        // Assert
        type.Should().Be("core.echo");
    }

    [Fact]
    public async Task ExecuteAsync_WithMessage_ShouldReturnMessage()
    {
        // Arrange
        var context = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "test-node",
            Parameters: new Dictionary<string, object?>
            {
                ["message"] = "Hello World"
            },
            Services: null!
        );

        // Act
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ActionExecutionStatus.Succeeded);
        result.Outputs.Should().ContainKey("echo");
        result.Outputs["echo"].Should().Be("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMessage_ShouldReturnDefaultEcho()
    {
        // Arrange
        var context = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "test-node",
            Parameters: new Dictionary<string, object?>(),
            Services: null!
        );

        // Act
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ActionExecutionStatus.Succeeded);
        result.Outputs.Should().ContainKey("echo");
        result.Outputs["echo"].Should().Be("echo");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullMessage_ShouldHandleGracefully()
    {
        // Arrange
        var context = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "test-node",
            Parameters: new Dictionary<string, object?>
            {
                ["message"] = null
            },
            Services: null!
        );

        // Act
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ActionExecutionStatus.Succeeded);
        result.Outputs.Should().ContainKey("echo");
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexMessage_ShouldReturnAsString()
    {
        // Arrange
        var complexObject = new { name = "test", value = 123 };
        var context = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "test-node",
            Parameters: new Dictionary<string, object?>
            {
                ["message"] = complexObject
            },
            Services: null!
        );

        // Act
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ActionExecutionStatus.Succeeded);
        result.Outputs.Should().ContainKey("echo");
        result.Outputs["echo"].Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleInvocations_ShouldBeIndependent()
    {
        // Arrange
        var context1 = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "node-1",
            Parameters: new Dictionary<string, object?> { ["message"] = "First" },
            Services: null!
        );
        var context2 = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "node-2",
            Parameters: new Dictionary<string, object?> { ["message"] = "Second" },
            Services: null!
        );

        // Act
        var result1 = await _action.ExecuteAsync(context1, CancellationToken.None);
        var result2 = await _action.ExecuteAsync(context2, CancellationToken.None);

        // Assert
        result1.Outputs["echo"].Should().Be("First");
        result2.Outputs["echo"].Should().Be("Second");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldStillComplete()
    {
        // Arrange
        var context = new ActionExecutionContext(
            WorkflowExecutionId: Guid.NewGuid(),
            NodeId: "test-node",
            Parameters: new Dictionary<string, object?> { ["message"] = "Test" },
            Services: null!
        );
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _action.ExecuteAsync(context, cts.Token);

        // Assert
        // Echo action completes synchronously, so cancellation doesn't affect it
        result.Status.Should().Be(ActionExecutionStatus.Succeeded);
    }
}
