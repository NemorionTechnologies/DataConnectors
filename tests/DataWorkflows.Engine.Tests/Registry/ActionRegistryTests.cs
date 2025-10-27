using DataWorkflows.Engine.Core.Application.Registry;
using DataWorkflows.Engine.Infrastructure.Actions;
using FluentAssertions;
using Xunit;

namespace DataWorkflows.Engine.Tests.Registry;

public class ActionRegistryTests
{
    [Fact]
    public void Constructor_ShouldCreateEmptyRegistry()
    {
        // Arrange & Act
        var registry = new ActionRegistry();

        // Assert
        Action act = () => registry.GetAction("core.echo");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetAction_ExistingAction_ShouldReturnAction()
    {
        // Arrange
        var registry = new ActionRegistry();
        var echoAction = new CoreEchoAction();
        registry.Register(echoAction);

        // Act
        var action = registry.GetAction("core.echo");

        // Assert
        action.Should().NotBeNull();
        action.Should().BeOfType<CoreEchoAction>();
    }

    [Fact]
    public void GetAction_NonExistingAction_ShouldThrow()
    {
        // Arrange
        var registry = new ActionRegistry();

        // Act
        Action act = () => registry.GetAction("nonexistent.action");

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void Register_NewAction_ShouldSucceed()
    {
        // Arrange
        var registry = new ActionRegistry();
        var testAction = new CoreEchoAction();

        // Act
        registry.Register(testAction);

        // Assert
        var retrieved = registry.GetAction("core.echo");
        retrieved.Should().BeSameAs(testAction);
    }

    [Fact]
    public void Register_DuplicateAction_ShouldOverwrite()
    {
        // Arrange
        var registry = new ActionRegistry();
        var action1 = new CoreEchoAction();
        var action2 = new CoreEchoAction();

        // Act
        registry.Register(action1);
        registry.Register(action2);

        // Assert
        var retrieved = registry.GetAction("core.echo");
        retrieved.Should().BeSameAs(action2);
    }
}
