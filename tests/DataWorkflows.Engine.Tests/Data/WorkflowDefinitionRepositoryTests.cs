using Xunit;
using DataWorkflows.Data.Repositories;
using System;
using System.Threading.Tasks;

namespace DataWorkflows.Engine.Tests.Data;

[Trait("Category", "Integration")]
[Trait("Category", "RequiresPostgres")]
public class WorkflowDefinitionRepositoryTests : IDisposable
{
    private readonly string _connectionString;
    private readonly WorkflowDefinitionRepository _repo;
    private readonly WorkflowRepository _workflowRepo;

    public WorkflowDefinitionRepositoryTests()
    {
        _connectionString = TestDatabase.GetConnectionString();
        _repo = new WorkflowDefinitionRepository(_connectionString);
        _workflowRepo = new WorkflowRepository(_connectionString);
    }

    public void Dispose()
    {
        TestDatabase.Cleanup(_connectionString);
    }

    private async Task<string> CreateWorkflow(string? id = null)
    {
        var workflowId = id ?? "test-workflow-" + Guid.NewGuid();
        await _workflowRepo.CreateDraftAsync(workflowId, "Test", "Test");
        return workflowId;
    }

    [Fact]
    public async Task CreateOrUpdateDraftAsync_CreatesNewDraft()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test\"}";

        // Act
        var (version, created) = await _repo.CreateOrUpdateDraftAsync(workflowId, json);
        var result = await _repo.GetDraftVersionAsync(workflowId);

        // Assert
        Assert.Equal(0, version);
        Assert.True(created);
        Assert.NotNull(result);
        Assert.Equal(workflowId, result.WorkflowId);
        Assert.Equal(0, result.Version);
        Assert.Equal(json, result.DefinitionJson);
    }

    [Fact]
    public async Task CreateOrUpdateDraftAsync_UpdatesExistingDraft()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json1 = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test 1\"}";
        var json2 = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test 2\"}";

        await _repo.CreateOrUpdateDraftAsync(workflowId, json1);

        // Act
        var (version, created) = await _repo.CreateOrUpdateDraftAsync(workflowId, json2);
        var result = await _repo.GetDraftVersionAsync(workflowId);

        // Assert
        Assert.Equal(0, version);
        Assert.False(created); // Updated, not created
        Assert.NotNull(result);
        Assert.Equal(json2, result.DefinitionJson);
    }

    [Fact]
    public async Task PublishVersionAsync_CreatesNewVersion()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test\"}";
        await _repo.CreateOrUpdateDraftAsync(workflowId, json);

        // Act
        var (version, created) = await _repo.PublishVersionAsync(workflowId, json);
        var result = await _repo.GetByIdAndVersionAsync(workflowId, version);

        // Assert
        Assert.Equal(1, version);
        Assert.True(created);
        Assert.NotNull(result);
        Assert.Equal(workflowId, result.WorkflowId);
        Assert.Equal(1, result.Version);
        Assert.Equal(json, result.DefinitionJson);
        Assert.NotNull(result.Checksum);
    }

    [Fact]
    public async Task PublishVersionAsync_IncrementsVersion()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json1 = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test 1\"}";
        var json2 = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test 2\"}";
        var json3 = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test 3\"}";

        // Act
        var (v1, created1) = await _repo.PublishVersionAsync(workflowId, json1);
        var (v2, created2) = await _repo.PublishVersionAsync(workflowId, json2);
        var (v3, created3) = await _repo.PublishVersionAsync(workflowId, json3);

        // Assert
        Assert.Equal(1, v1);
        Assert.True(created1);
        Assert.Equal(2, v2);
        Assert.True(created2);
        Assert.Equal(3, v3);
        Assert.True(created3);
    }

    [Fact]
    public async Task PublishVersionAsync_IdempotentForSameContent()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test\"}";

        // Act
        var (v1, created1) = await _repo.PublishVersionAsync(workflowId, json);
        var (v2, created2) = await _repo.PublishVersionAsync(workflowId, json);
        var (v3, created3) = await _repo.PublishVersionAsync(workflowId, json);

        // Assert
        Assert.Equal(1, v1);
        Assert.True(created1);
        Assert.Equal(1, v2);
        Assert.False(created2); // Reused
        Assert.Equal(1, v3);
        Assert.False(created3); // Reused
    }

    [Fact]
    public async Task PublishVersionAsync_DetectsDifferentContentWithWhitespace()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json1 = "{\"id\":\"" + workflowId + "\",\"displayName\":\"Test\"}";
        var json2 = "{\n  \"id\": \"" + workflowId + "\",\n  \"displayName\": \"Test\"\n}";

        // Act
        var (v1, created1) = await _repo.PublishVersionAsync(workflowId, json1);
        var (v2, created2) = await _repo.PublishVersionAsync(workflowId, json2);

        // Assert
        Assert.Equal(1, v1);
        Assert.True(created1);
        Assert.Equal(2, v2);
        Assert.True(created2); // Different checksum due to whitespace
    }

    [Fact]
    public async Task GetByIdAndVersionAsync_ReturnsCorrectVersion()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json1 = "{\"id\": \"" + workflowId + "\", \"version\": 1}";
        var json2 = "{\"id\": \"" + workflowId + "\", \"version\": 2}";

        await _repo.PublishVersionAsync(workflowId, json1);
        await _repo.PublishVersionAsync(workflowId, json2);

        // Act
        var result1 = await _repo.GetByIdAndVersionAsync(workflowId, 1);
        var result2 = await _repo.GetByIdAndVersionAsync(workflowId, 2);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal(json1, result1.DefinitionJson);
        Assert.NotNull(result2);
        Assert.Equal(json2, result2.DefinitionJson);
    }

    [Fact]
    public async Task GetByIdAndVersionAsync_ReturnsNullForNonExistent()
    {
        // Arrange
        var workflowId = await CreateWorkflow();

        // Act
        var result = await _repo.GetByIdAndVersionAsync(workflowId, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestVersionAsync_ReturnsHighestVersion()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json1 = "{\"id\": \"" + workflowId + "\", \"version\": 1}";
        var json2 = "{\"id\": \"" + workflowId + "\", \"version\": 2}";
        var json3 = "{\"id\": \"" + workflowId + "\", \"version\": 3}";

        await _repo.PublishVersionAsync(workflowId, json1);
        await _repo.PublishVersionAsync(workflowId, json2);
        await _repo.PublishVersionAsync(workflowId, json3);

        // Act
        var result = await _repo.GetLatestVersionAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Version);
        Assert.Equal(json3, result.DefinitionJson);
    }

    [Fact]
    public async Task GetLatestVersionAsync_ReturnsNullWhenNoVersions()
    {
        // Arrange
        var workflowId = await CreateWorkflow();

        // Act
        var result = await _repo.GetLatestVersionAsync(workflowId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDraftVersionAsync_ReturnsDraftVersion()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var draftJson = "{\"id\": \"" + workflowId + "\", \"status\": \"draft\"}";
        var publishedJson = "{\"id\": \"" + workflowId + "\", \"status\": \"published\"}";

        await _repo.CreateOrUpdateDraftAsync(workflowId, draftJson);
        await _repo.PublishVersionAsync(workflowId, publishedJson);

        // Act
        var result = await _repo.GetDraftVersionAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Version);
        Assert.Equal(draftJson, result.DefinitionJson);
    }

    [Fact]
    public async Task GetAllVersionsAsync_ReturnsAllVersions()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var draftJson = "{\"id\": \"" + workflowId + "\", \"draft\": true}";
        var json1 = "{\"id\": \"" + workflowId + "\", \"version\": 1}";
        var json2 = "{\"id\": \"" + workflowId + "\", \"version\": 2}";

        await _repo.CreateOrUpdateDraftAsync(workflowId, draftJson);
        await _repo.PublishVersionAsync(workflowId, json1);
        await _repo.PublishVersionAsync(workflowId, json2);

        // Act
        var results = await _repo.GetAllVersionsAsync(workflowId);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.Version == 0);
        Assert.Contains(results, r => r.Version == 1);
        Assert.Contains(results, r => r.Version == 2);
    }

    [Fact]
    public async Task DeleteDraftAsync_DeletesDraftVersion()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var draftJson = "{\"id\": \"" + workflowId + "\", \"draft\": true}";
        var publishedJson = "{\"id\": \"" + workflowId + "\", \"published\": true}";

        await _repo.CreateOrUpdateDraftAsync(workflowId, draftJson);
        await _repo.PublishVersionAsync(workflowId, publishedJson);

        // Act
        await _repo.DeleteDraftAsync(workflowId);
        var draftResult = await _repo.GetDraftVersionAsync(workflowId);
        var publishedResult = await _repo.GetByIdAndVersionAsync(workflowId, 1);

        // Assert
        Assert.Null(draftResult); // Draft deleted
        Assert.NotNull(publishedResult); // Published version still exists
    }

    [Fact]
    public async Task ChecksumIsConsistent_ForSameContent()
    {
        // Arrange
        var workflowId = await CreateWorkflow();
        var json = "{\"id\": \"" + workflowId + "\", \"displayName\": \"Test\"}";

        // Act
        await _repo.PublishVersionAsync(workflowId, json);
        var result1 = await _repo.GetByIdAndVersionAsync(workflowId, 1);

        // Publish same content again (should be idempotent)
        await _repo.PublishVersionAsync(workflowId, json);
        var result2 = await _repo.GetByIdAndVersionAsync(workflowId, 1);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.Checksum, result2.Checksum);
    }
}
