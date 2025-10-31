using DataWorkflows.Connector.Monday.Application.DTOs;
using DataWorkflows.Connector.Monday.Application.Filters;
using DataWorkflows.Connector.Monday.Infrastructure.Filtering;
using FluentAssertions;

namespace DataWorkflows.Connector.Monday.Tests.Filtering;

public class MondayItemFilterProcessorTests
{
    [Fact]
    public async Task ApplySubItemFilterAsync_ReturnsParentsWithMatchingSubItems()
    {
        var parentItems = new List<MondayItemDto>
        {
            new() { Id = "parent-1" },
            new() { Id = "parent-2" }
        };

        var subItemsLookup = new Dictionary<string, IEnumerable<MondayItemDto>>
        {
            ["parent-1"] = new[] { new MondayItemDto { Id = "sub-match", Title = "Match" } },
            ["parent-2"] = new[] { new MondayItemDto { Id = "sub-miss", Title = "Miss" } }
        };

        var updatesLookup = new Dictionary<string, IEnumerable<MondayUpdateDto>>
        {
            ["sub-match"] = new[] { new MondayUpdateDto { Id = "u1", ItemId = "sub-match", BodyText = "ok" } },
            ["sub-miss"] = Array.Empty<MondayUpdateDto>()
        };

        var processor = new MondayItemFilterProcessor(
            (itemId, _, _) => Task.FromResult(subItemsLookup.TryGetValue(itemId, out var items)
                ? items
                : Enumerable.Empty<MondayItemDto>()),
            (itemId, _, _, _) => Task.FromResult(updatesLookup.TryGetValue(itemId, out var updates)
                ? updates
                : Enumerable.Empty<MondayUpdateDto>()));

        var translation = new MondaySubItemFilterTranslation(
            item => item.Title.Contains("Match"),
            updates => updates.Any(u => u.BodyText == "ok"),
            logs => logs.Any(),
            MondayAggregationMode.Any);

        var activityLookup = new Dictionary<string, IReadOnlyList<MondayActivityLogDto>>
        {
            ["sub-match"] = new List<MondayActivityLogDto> { new() { ItemId = "sub-match" } }
        };

        var result = await processor.ApplySubItemFilterAsync(
            parentItems,
            translation,
            CancellationToken.None,
            activityLookup);

        result.Should().ContainSingle(item => item.Id == "parent-1");
    }

    [Fact]
    public async Task ApplySubItemFilterAsync_AllModeRequiresAllMatches()
    {
        var parentItems = new List<MondayItemDto>
        {
            new() { Id = "parent-1" }
        };

        var subItemsLookup = new Dictionary<string, IEnumerable<MondayItemDto>>
        {
            ["parent-1"] = new[]
            {
                new MondayItemDto { Id = "sub-1", Title = "Match" },
                new MondayItemDto { Id = "sub-2", Title = "Other" }
            }
        };

        var processor = new MondayItemFilterProcessor(
            (itemId, _, _) => Task.FromResult(subItemsLookup[itemId]),
            (_, _, _, _) => Task.FromResult<IEnumerable<MondayUpdateDto>>(Array.Empty<MondayUpdateDto>()));

        var translation = new MondaySubItemFilterTranslation(
            item => item.Title.Contains("Match"),
            null,
            null,
            MondayAggregationMode.All);

        var result = await processor.ApplySubItemFilterAsync(
            parentItems,
            translation,
            CancellationToken.None,
            activityLogLookup: null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyUpdateFilterAsync_ReturnsMatchingParents()
    {
        var parentItems = new List<MondayItemDto>
        {
            new() { Id = "parent-1" },
            new() { Id = "parent-2" }
        };

        var updatesLookup = new Dictionary<string, IEnumerable<MondayUpdateDto>>
        {
            ["parent-1"] = new[] { new MondayUpdateDto { Id = "u1", ItemId = "parent-1", BodyText = "keep" } },
            ["parent-2"] = new[] { new MondayUpdateDto { Id = "u2", ItemId = "parent-2", BodyText = "drop" } }
        };

        var processor = new MondayItemFilterProcessor(
            (_, _, _) => Task.FromResult<IEnumerable<MondayItemDto>>(Array.Empty<MondayItemDto>()),
            (itemId, _, _, _) => Task.FromResult(updatesLookup[itemId]));

        var result = await processor.ApplyUpdateFilterAsync(
            parentItems,
            updates => updates.Any(u => u.BodyText == "keep"),
            CancellationToken.None);

        result.Should().ContainSingle(item => item.Id == "parent-1");
    }

    [Fact]
    public void ApplyActivityLogFilter_ReturnsItemsMatchingPredicate()
    {
        var items = new List<MondayItemDto>
        {
            new() { Id = "1" },
            new() { Id = "2" }
        };

        var lookup = new Dictionary<string, IReadOnlyList<MondayActivityLogDto>>
        {
            ["1"] = new List<MondayActivityLogDto> { new() { ItemId = "1" } }
        };

        var processor = new MondayItemFilterProcessor(
            (_, _, _) => Task.FromResult<IEnumerable<MondayItemDto>>(Array.Empty<MondayItemDto>()),
            (_, _, _, _) => Task.FromResult<IEnumerable<MondayUpdateDto>>(Array.Empty<MondayUpdateDto>()));

        var result = processor.ApplyActivityLogFilter(
            items,
            logs => logs.Any(),
            lookup);

        result.Should().ContainSingle(item => item.Id == "1");
    }

    [Fact]
    public async Task ApplySubItemFilterAsync_WhenNoParents_ReturnsEmpty()
    {
        var processor = new MondayItemFilterProcessor(
            (_, _, _) => Task.FromResult<IEnumerable<MondayItemDto>>(Array.Empty<MondayItemDto>()),
            (_, _, _, _) => Task.FromResult<IEnumerable<MondayUpdateDto>>(Array.Empty<MondayUpdateDto>()));

        var result = await processor.ApplySubItemFilterAsync(
            new List<MondayItemDto>(),
            new MondaySubItemFilterTranslation(null, null, null, MondayAggregationMode.Any),
            CancellationToken.None,
            activityLogLookup: null);

        result.Should().BeEmpty();
    }
}
