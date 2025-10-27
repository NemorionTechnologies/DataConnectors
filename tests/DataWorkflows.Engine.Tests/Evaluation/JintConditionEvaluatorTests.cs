using DataWorkflows.Engine.Core.Application.Evaluation;
using FluentAssertions;
using System.Collections.ObjectModel;
using Xunit;

namespace DataWorkflows.Engine.Tests.Evaluation;

public class JintConditionEvaluatorTests
{
    private readonly JintConditionEvaluator _evaluator;

    public JintConditionEvaluatorTests()
    {
        _evaluator = new JintConditionEvaluator();
    }

    [Fact]
    public void Evaluate_EmptyCondition_ShouldReturnTrue()
    {
        // Arrange
        var condition = "";
        var scope = CreateScope();

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NullCondition_ShouldReturnTrue()
    {
        // Arrange
        string? condition = null;
        var scope = CreateScope();

        // Act
        var result = _evaluator.Evaluate(condition!, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_TrueLiteral_ShouldReturnTrue()
    {
        // Arrange
        var condition = "true";
        var scope = CreateScope();

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_FalseLiteral_ShouldReturnFalse()
    {
        // Arrange
        var condition = "false";
        var scope = CreateScope();

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_SimpleEquality_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.status === 'approved'";
        var scope = CreateScope(new { status = "approved" });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_SimpleEquality_ShouldReturnFalse()
    {
        // Arrange
        var condition = "trigger.status === 'approved'";
        var scope = CreateScope(new { status = "rejected" });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NotEquality_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.status !== 'approved'";
        var scope = CreateScope(new { status = "rejected" });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NumericComparison_GreaterThan_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.count > 5";
        var scope = CreateScope(new { count = 10 });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NumericComparison_LessThan_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.priority < 100";
        var scope = CreateScope(new { priority = 50 });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LogicalAnd_BothTrue_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.enabled && trigger.verified";
        var scope = CreateScope(new { enabled = true, verified = true });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LogicalAnd_OneFalse_ShouldReturnFalse()
    {
        // Arrange
        var condition = "trigger.enabled && trigger.verified";
        var scope = CreateScope(new { enabled = true, verified = false });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LogicalOr_OneTrue_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.isAdmin || trigger.isModerator";
        var scope = CreateScope(new { isAdmin = false, isModerator = true });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_LogicalNot_ShouldReturnTrue()
    {
        // Arrange
        var condition = "!trigger.disabled";
        var scope = CreateScope(new { disabled = false });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ComplexExpression_ShouldReturnTrue()
    {
        // Arrange
        var condition = "(trigger.status === 'approved' && trigger.count > 5) || trigger.priority === 'high'";
        var scope = CreateScope(new { status = "pending", count = 3, priority = "high" });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ContextDataAccess_ShouldReturnTrue()
    {
        // Arrange
        var condition = "context.data['step1'] !== null";
        var contextData = new Dictionary<string, object?>
        {
            ["step1"] = new { result = "success" }
        };
        var scope = CreateScope(contextData: contextData);

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_VarsAccess_ShouldReturnTrue()
    {
        // Arrange
        var condition = "vars.environment === 'production'";
        var varsData = new Dictionary<string, object?>
        {
            ["environment"] = "production"
        };
        var scope = CreateScope(vars: varsData);

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_InvalidSyntax_ShouldReturnFalse()
    {
        // Arrange
        var condition = "invalid syntax ===";
        var scope = CreateScope();

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_UndefinedVariable_ShouldReturnFalse()
    {
        // Arrange
        var condition = "trigger.nonexistent === 'value'";
        var scope = CreateScope(new { });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NullPropertyAccess_ShouldReturnFalse()
    {
        // Arrange
        var condition = "trigger.nested.property === 'value'";
        var scope = CreateScope(new { nested = (object?)null });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ArrayLengthCheck_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.items.length > 2";
        var scope = CreateScope(new { items = new[] { 1, 2, 3 } });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_TernaryOperator_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.count > 5 ? true : false";
        var scope = CreateScope(new { count = 10 });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringIncludes_ShouldReturnTrue()
    {
        // Arrange
        var condition = "trigger.message.includes('error')";
        var scope = CreateScope(new { message = "An error occurred" });

        // Act
        var result = _evaluator.Evaluate(condition, scope);

        // Assert
        result.Should().BeTrue();
    }

    private ConditionScope CreateScope(
        object? trigger = null,
        Dictionary<string, object?>? contextData = null,
        Dictionary<string, object?>? vars = null)
    {
        var triggerDict = trigger != null
            ? trigger.GetType().GetProperties()
                .ToDictionary(p => p.Name, p => p.GetValue(trigger))
            : new Dictionary<string, object?>();

        var contextDict = contextData ?? new Dictionary<string, object?>();
        var varsDict = vars ?? new Dictionary<string, object?>();

        return new ConditionScope(
            new ReadOnlyDictionary<string, object?>(triggerDict),
            new ReadOnlyDictionary<string, object?>(contextDict),
            new ReadOnlyDictionary<string, object?>(varsDict)
        );
    }
}
