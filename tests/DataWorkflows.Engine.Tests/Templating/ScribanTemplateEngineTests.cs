using DataWorkflows.Engine.Core.Application.Templating;
using FluentAssertions;
using Xunit;

namespace DataWorkflows.Engine.Tests.Templating;

public class ScribanTemplateEngineTests
{
    private readonly ScribanTemplateEngine _engine;

    public ScribanTemplateEngineTests()
    {
        var options = new TemplateEngineOptions
        {
            RenderTimeoutMs = 2000,
            StrictMode = true,
            EnableLoops = false,
            EnableFunctions = false
        };
        _engine = new ScribanTemplateEngine(options);
    }

    [Fact]
    public async Task RenderAsync_SimpleString_ShouldSucceed()
    {
        // Arrange
        var template = "Hello World";
        var model = new { };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public async Task RenderAsync_SimpleVariable_ShouldSucceed()
    {
        // Arrange
        var template = "Hello {{ name }}";
        var model = new { name = "Alice" };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("Hello Alice");
    }

    [Fact]
    public async Task RenderAsync_NestedProperty_ShouldSucceed()
    {
        // Arrange
        var template = "User: {{ user.name }}, Email: {{ user.email }}";
        var model = new
        {
            user = new
            {
                name = "Bob",
                email = "bob@example.com"
            }
        };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("User: Bob, Email: bob@example.com");
    }

    [Fact]
    public async Task RenderAsync_ArrayAccess_ShouldSucceed()
    {
        // Arrange
        var template = "First: {{ items[0] }}, Second: {{ items[1] }}";
        var model = new
        {
            items = new[] { "Apple", "Banana", "Cherry" }
        };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("First: Apple, Second: Banana");
    }

    [Fact]
    public async Task RenderAsync_ConditionalExpression_ShouldSucceed()
    {
        // Arrange
        var template = "Status: {{ if approved }}Approved{{ else }}Pending{{ end }}";
        var model = new { approved = true };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("Status: Approved");
    }

    [Fact]
    public async Task RenderAsync_JsonTemplate_ShouldSucceed()
    {
        // Arrange
        var template = """
        {
          "message": "Hello {{ trigger.username }}",
          "requestId": "{{ trigger.requestId }}"
        }
        """;
        var model = new
        {
            trigger = new
            {
                username = "charlie",
                requestId = "req-123"
            }
        };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Contain("\"message\": \"Hello charlie\"");
        result.Should().Contain("\"requestId\": \"req-123\"");
    }

    [Fact]
    public async Task RenderAsync_ContextDataAccess_ShouldSucceed()
    {
        // Arrange
        var template = "Previous: {{ context.data['step1'].echo }}";
        var model = new
        {
            context = new
            {
                data = new Dictionary<string, object>
                {
                    ["step1"] = new { echo = "Hello from step1" }
                }
            }
        };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("Previous: Hello from step1");
    }

    [Fact]
    public async Task RenderAsync_NullValue_StrictMode_ShouldThrow()
    {
        // Arrange
        var template = "Value: {{ missing }}";
        var model = new { };

        // Act
        Func<Task> act = async () => await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RenderAsync_Loop_ShouldThrow()
    {
        // Arrange
        var template = "{{ for item in items }}{{ item }}{{ end }}";
        var model = new { items = new[] { 1, 2, 3 } };

        // Act
        Func<Task> act = async () => await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Loops*disabled*");
    }

    [Fact]
    public async Task RenderAsync_WhileLoop_ShouldThrow()
    {
        // Arrange
        var template = "{{ while count < 5 }}{{ count }}{{ count = count + 1 }}{{ end }}";
        var model = new { count = 0 };

        // Act
        Func<Task> act = async () => await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Loops*disabled*");
    }

    [Fact]
    public async Task RenderAsync_Function_ShouldThrow()
    {
        // Arrange
        var template = "{{ string.upcase 'hello' }}";
        var model = new { };

        // Act
        Func<Task> act = async () => await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Functions*disabled*");
    }

    [Fact]
    public async Task RenderAsync_InvalidSyntax_ShouldThrow()
    {
        // Arrange
        var template = "{{ invalid syntax }}}}";
        var model = new { };

        // Act
        Func<Task> act = async () => await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RenderAsync_EmptyTemplate_ShouldReturnEmpty()
    {
        // Arrange
        var template = "";
        var model = new { };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RenderAsync_ComplexNestedTemplate_ShouldSucceed()
    {
        // Arrange
        var template = "{{ trigger.data.user.profile.settings.email }}";
        var model = new
        {
            trigger = new
            {
                data = new
                {
                    user = new
                    {
                        profile = new
                        {
                            settings = new
                            {
                                email = "deep@example.com"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("deep@example.com");
    }

    [Fact]
    public async Task RenderAsync_MathExpression_ShouldSucceed()
    {
        // Arrange
        var template = "Result: {{ a + b }}";
        var model = new { a = 10, b = 20 };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("Result: 30");
    }

    [Fact]
    public async Task RenderAsync_ComparisonExpression_ShouldSucceed()
    {
        // Arrange
        var template = "{{ if count > 5 }}High{{ else }}Low{{ end }}";
        var model = new { count = 10 };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("High");
    }

    [Fact]
    public async Task RenderAsync_StringConcatenation_ShouldSucceed()
    {
        // Arrange
        var template = "{{ first + ' ' + last }}";
        var model = new { first = "John", last = "Doe" };

        // Act
        var result = await _engine.RenderAsync(template, model, CancellationToken.None);

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public async Task RenderAsync_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        var template = "{{ trigger.data }}";
        var model = new { trigger = new { data = "test" } };
        var cts = new CancellationTokenSource();

        // Act - For fast templates, they complete before cancellation can occur
        // This test verifies the token is passed through without error
        var result = await _engine.RenderAsync(template, model, cts.Token);

        // Assert
        result.Should().Be("test");
    }
}
