using System;
using System.Text.Json;

namespace DataWorkflows.Connector.Monday.Infrastructure.Parsing;

internal class ActivityLogParser
{
    public string? TryExtractActivityItemId(string? data)
    {
        if (string.IsNullOrWhiteSpace(data) || data == "{}")
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (TryGetString(root, "item_id", out var itemId) ||
                TryGetString(root, "itemId", out itemId) ||
                TryGetString(root, "pulseId", out itemId) ||
                TryGetString(root, "entity_id", out itemId) ||
                TryGetString(root, "entityId", out itemId))
            {
                return itemId;
            }

            if (root.TryGetProperty("entity", out var entity) &&
                (TryGetString(entity, "id", out itemId) ||
                 TryGetString(entity, "item_id", out itemId)))
            {
                return itemId;
            }

            if (root.TryGetProperty("item", out var itemElement) &&
                TryGetString(itemElement, "id", out itemId))
            {
                return itemId;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed data payloads
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            switch (property.ValueKind)
            {
                case JsonValueKind.String:
                    value = property.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                case JsonValueKind.Number when property.TryGetInt64(out var number):
                    value = number.ToString();
                    return true;
            }
        }

        value = null;
        return false;
    }
}
