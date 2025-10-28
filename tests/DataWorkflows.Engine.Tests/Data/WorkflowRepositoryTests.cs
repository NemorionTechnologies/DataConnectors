using Xunit;
using DataWorkflows.Data.Repositories;
using System;
using System.Threading.Tasks;

namespace DataWorkflows.Engine.Tests.Data;

[Trait("Category", "Integration")]
[Trait("Category", "RequiresPostgres")]
public class WorkflowRepositoryTests : IDisposable
{
    private readonly string _connectionString;
    private readonly WorkflowRepository _repo;

    public WorkflowRepositoryTests()
    {
        _connectionString = TestDatabase.GetConnectionString();
        _repo = new WorkflowRepository(_connectionString);
    }

    public void Dispose()
    {
        TestDatabase.Cleanup(_connectionString);
    }

    [Fact]
    public async Task CreateDraftAsync_CreatesNewWorkflow()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        var displayName = "Test Workflow";
        var description = "Test Description";

        // Act
        await _repo.CreateDraftAsync(workflowId, displayName, description);
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflowId, result.Id);
        Assert.Equal(displayName, result.DisplayName);
        Assert.Equal(description, result.Description);
        Assert.Equal("Draft", result.Status);
        Assert.Null(result.CurrentVersion);
        Assert.True(result.IsEnabled);
        Assert.True(result.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(result.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task UpdateDraftAsync_UpdatesExistingDraft()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Original Name", "Original Description");
        await Task.Delay(100); // Ensure UpdatedAt will be different

        // Act
        await _repo.UpdateDraftAsync(workflowId, "Updated Name", "Updated Description");
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.DisplayName);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal("Draft", result.Status);
        Assert.True(result.UpdatedAt > result.CreatedAt);
    }

    [Fact]
    public async Task UpdateDraftAsync_ThrowsWhenWorkflowNotFound()
    {
        // Arrange
        var workflowId = "non-existent-workflow";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.UpdateDraftAsync(workflowId, "Name", "Description"));
    }

    [Fact]
    public async Task UpdateDraftAsync_ThrowsWhenWorkflowIsNotDraft()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");
        await _repo.PublishAsync(workflowId, 1, autoActivate: true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.UpdateDraftAsync(workflowId, "Updated", "Updated"));
    }

    [Fact]
    public async Task PublishAsync_TransitionsDraftToActive()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");

        // Act
        await _repo.PublishAsync(workflowId, 1, autoActivate: true);
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);
        Assert.Equal(1, result.CurrentVersion);
        Assert.True(result.IsEnabled);
    }

    [Fact]
    public async Task PublishAsync_WithAutoActivateFalse_KeepsStatusDraft()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");

        // Act
        await _repo.PublishAsync(workflowId, 1, autoActivate: false);
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(1, result.CurrentVersion);
        Assert.True(result.IsEnabled);
    }

    [Fact]
    public async Task ArchiveAsync_TransitionsActiveToArchived()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");
        await _repo.PublishAsync(workflowId, 1, autoActivate: true);

        // Act
        await _repo.ArchiveAsync(workflowId);
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Archived", result.Status);
        Assert.False(result.IsEnabled);
    }

    [Fact]
    public async Task ArchiveAsync_ThrowsWhenWorkflowNotFound()
    {
        // Arrange
        var workflowId = "non-existent-workflow";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.ArchiveAsync(workflowId));
    }

    [Fact]
    public async Task ArchiveAsync_ThrowsWhenWorkflowIsDraft()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.ArchiveAsync(workflowId));
        Assert.Contains("Active", ex.Message);
    }

    [Fact]
    public async Task ReactivateAsync_TransitionsArchivedToActive()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");
        await _repo.PublishAsync(workflowId, 1, autoActivate: true);
        await _repo.ArchiveAsync(workflowId);

        // Act
        await _repo.ReactivateAsync(workflowId);
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);
        Assert.True(result.IsEnabled);
    }

    [Fact]
    public async Task ReactivateAsync_ThrowsWhenWorkflowNotArchived()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");
        await _repo.PublishAsync(workflowId, 1, autoActivate: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.ReactivateAsync(workflowId));
        Assert.Contains("Archived", ex.Message);
    }

    [Fact]
    public async Task DeleteDraftAsync_DeletesDraftWorkflow()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");

        // Act
        await _repo.DeleteDraftAsync(workflowId);
        var result = await _repo.GetByIdAsync(workflowId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteDraftAsync_ThrowsWhenWorkflowIsNotDraft()
    {
        // Arrange
        var workflowId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId, "Test", "Test");
        await _repo.PublishAsync(workflowId, 1, autoActivate: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repo.DeleteDraftAsync(workflowId));
        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllWorkflows()
    {
        // Arrange
        var workflowId1 = "test-workflow-" + Guid.NewGuid();
        var workflowId2 = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(workflowId1, "Test 1", "Test 1");
        await _repo.CreateDraftAsync(workflowId2, "Test 2", "Test 2");
        await _repo.PublishAsync(workflowId2, 1, autoActivate: true);

        // Act
        var results = await _repo.GetAllAsync();

        // Assert
        Assert.Contains(results, w => w.Id == workflowId1);
        Assert.Contains(results, w => w.Id == workflowId2);
    }

    [Fact]
    public async Task GetAllAsync_FiltersbyStatus()
    {
        // Arrange
        var draftId = "test-workflow-" + Guid.NewGuid();
        var activeId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(draftId, "Draft", "Draft");
        await _repo.CreateDraftAsync(activeId, "Active", "Active");
        await _repo.PublishAsync(activeId, 1, autoActivate: true);

        // Act
        var draftResults = await _repo.GetAllAsync(status: "Draft");
        var activeResults = await _repo.GetAllAsync(status: "Active");

        // Assert
        Assert.Contains(draftResults, w => w.Id == draftId);
        Assert.DoesNotContain(draftResults, w => w.Id == activeId);
        Assert.Contains(activeResults, w => w.Id == activeId);
        Assert.DoesNotContain(activeResults, w => w.Id == draftId);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByIsEnabled()
    {
        // Arrange
        var enabledId = "test-workflow-" + Guid.NewGuid();
        var disabledId = "test-workflow-" + Guid.NewGuid();
        await _repo.CreateDraftAsync(enabledId, "Enabled", "Enabled");
        await _repo.CreateDraftAsync(disabledId, "Disabled", "Disabled");
        await _repo.PublishAsync(enabledId, 1, autoActivate: true);
        await _repo.PublishAsync(disabledId, 1, autoActivate: true);
        await _repo.ArchiveAsync(disabledId);

        // Act
        var enabledResults = await _repo.GetAllAsync(isEnabled: true);
        var disabledResults = await _repo.GetAllAsync(isEnabled: false);

        // Assert
        Assert.Contains(enabledResults, w => w.Id == enabledId);
        Assert.DoesNotContain(enabledResults, w => w.Id == disabledId);
        Assert.Contains(disabledResults, w => w.Id == disabledId);
        Assert.DoesNotContain(disabledResults, w => w.Id == enabledId);
    }
}
