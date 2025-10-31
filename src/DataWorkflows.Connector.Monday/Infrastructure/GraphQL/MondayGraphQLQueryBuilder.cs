using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using DataWorkflows.Connector.Monday.Application.Filters;

namespace DataWorkflows.Connector.Monday.Infrastructure.GraphQL;

internal class MondayGraphQLQueryBuilder
{
    public string BuildGetBoardItemsQuery(
        string boardId,
        MondayFilterDefinition? filter,
        MondayQueryParams? queryParams)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter?.GroupId))
        {
            arguments.Add($"groups: [\"{filter!.GroupId}\"]");
        }

        if (queryParams is not null && queryParams.HasRules)
        {
            arguments.Add($"query_params: {BuildQueryParamsFragment(queryParams)}");
        }

        var itemsPageArguments = arguments.Count > 0
            ? $"({string.Join(", ", arguments)})"
            : string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("    boards(ids: [").Append(boardId).AppendLine("]) {");
        builder.Append("        items_page").Append(itemsPageArguments).AppendLine(" {");
        builder.AppendLine("            items {");
        builder.AppendLine("                id");
        builder.AppendLine("                name");
        builder.AppendLine("                group { id }");
        builder.AppendLine("                created_at");
        builder.AppendLine("                updated_at");
        builder.AppendLine("                parent_item { id }");
        builder.AppendLine("                column_values {");
        builder.AppendLine("                    id");
        builder.AppendLine("                    value");
        builder.AppendLine("                    text");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildGetBoardActivityQuery(string boardId, DateTime? fromDate, DateTime? toDate)
    {
        var dateFilter = string.Empty;
        if (fromDate.HasValue || toDate.HasValue)
        {
            var from = fromDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "1970-01-01T00:00:00Z";
            var to = toDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            dateFilter = $", from: \"{from}\", to: \"{to}\"";
        }

        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("    boards(ids: [").Append(boardId).AppendLine("]) {");
        builder.Append("        activity_logs").Append(dateFilter).AppendLine(" {");
        builder.AppendLine("            event");
        builder.AppendLine("            user_id");
        builder.AppendLine("            created_at");
        builder.AppendLine("            data");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildGetBoardUpdatesQuery(string boardId, DateTime? fromDate, DateTime? toDate)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("    boards(ids: [").Append(boardId).AppendLine("]) {");
        builder.AppendLine("        items_page {");
        builder.AppendLine("            items {");
        builder.AppendLine("                id");
        builder.AppendLine("                updates {");
        builder.AppendLine("                    id");
        builder.AppendLine("                    body");
        builder.AppendLine("                    creator_id");
        builder.AppendLine("                    created_at");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildGetSubItemsQuery(string parentItemId)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("    items(ids: [").Append(parentItemId).AppendLine("]) {");
        builder.AppendLine("        subitems {");
        builder.AppendLine("            id");
        builder.AppendLine("            name");
        builder.AppendLine("            group { id }");
        builder.AppendLine("            created_at");
        builder.AppendLine("            updated_at");
        builder.AppendLine("            parent_item { id }");
        builder.AppendLine("            column_values {");
        builder.AppendLine("                id");
        builder.AppendLine("                value");
        builder.AppendLine("                text");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildGetItemUpdatesQuery(string itemId, DateTime? fromDate, DateTime? toDate)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("    items(ids: [").Append(itemId).AppendLine("]) {");
        builder.AppendLine("        updates {");
        builder.AppendLine("            id");
        builder.AppendLine("            body");
        builder.AppendLine("            creator_id");
        builder.AppendLine("            created_at");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildUpdateColumnValueMutation(string boardId, string itemId, string columnId, string valueJson)
    {
        var escapedValue = EscapeGraphQlString(valueJson);
        var escapedColumnId = EscapeGraphQlString(columnId);

        var builder = new StringBuilder();
        builder.AppendLine("mutation {");
        builder.AppendLine("    change_column_value(");
        builder.Append("        board_id: ").Append(boardId).AppendLine(",");
        builder.Append("        item_id: ").Append(itemId).AppendLine(",");
        builder.Append("        column_id: \"").Append(escapedColumnId).AppendLine("\",");
        builder.Append("        value: \"").Append(escapedValue).AppendLine("\"");
        builder.AppendLine("    ) {");
        builder.AppendLine("        id");
        builder.AppendLine("        name");
        builder.AppendLine("        group { id }");
        builder.AppendLine("        created_at");
        builder.AppendLine("        updated_at");
        builder.AppendLine("        parent_item { id }");
        builder.AppendLine("        column_values {");
        builder.AppendLine("            id");
        builder.AppendLine("            value");
        builder.AppendLine("            text");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildCreateItemMutation(string boardId, string itemName, string? groupId, Dictionary<string, object>? columnValues)
    {
        var escapedItemName = EscapeGraphQlString(itemName);
        var groupIdArg = !string.IsNullOrWhiteSpace(groupId) ? $", group_id: \"{EscapeGraphQlString(groupId)}\"" : string.Empty;
        var columnValuesArg = string.Empty;

        if (columnValues != null && columnValues.Count > 0)
        {
            var columnValuesJson = JsonSerializer.Serialize(columnValues);
            var escapedColumnValues = EscapeGraphQlString(columnValuesJson);
            columnValuesArg = $", column_values: \"{escapedColumnValues}\"";
        }

        var builder = new StringBuilder();
        builder.AppendLine("mutation {");
        builder.AppendLine("    create_item(");
        builder.Append("        board_id: ").Append(boardId).AppendLine(",");
        builder.Append("        item_name: \"").Append(escapedItemName).Append("\"");
        builder.Append(groupIdArg);
        builder.Append(columnValuesArg);
        builder.AppendLine();
        builder.AppendLine("    ) {");
        builder.AppendLine("        id");
        builder.AppendLine("        name");
        builder.AppendLine("        group { id }");
        builder.AppendLine("        created_at");
        builder.AppendLine("        updated_at");
        builder.AppendLine("        parent_item { id }");
        builder.AppendLine("        column_values {");
        builder.AppendLine("            id");
        builder.AppendLine("            value");
        builder.AppendLine("            text");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildCreateSubItemMutation(string parentItemId, string itemName, Dictionary<string, object>? columnValues)
    {
        var escapedItemName = EscapeGraphQlString(itemName);
        var columnValuesArg = string.Empty;

        if (columnValues != null && columnValues.Count > 0)
        {
            var columnValuesJson = JsonSerializer.Serialize(columnValues);
            var escapedColumnValues = EscapeGraphQlString(columnValuesJson);
            columnValuesArg = $", column_values: \"{escapedColumnValues}\"";
        }

        var builder = new StringBuilder();
        builder.AppendLine("mutation {");
        builder.AppendLine("    create_subitem(");
        builder.Append("        parent_item_id: ").Append(parentItemId).AppendLine(",");
        builder.Append("        item_name: \"").Append(escapedItemName).Append("\"");
        builder.Append(columnValuesArg);
        builder.AppendLine();
        builder.AppendLine("    ) {");
        builder.AppendLine("        id");
        builder.AppendLine("        name");
        builder.AppendLine("        group { id }");
        builder.AppendLine("        created_at");
        builder.AppendLine("        updated_at");
        builder.AppendLine("        parent_item { id }");
        builder.AppendLine("        board { id }");
        builder.AppendLine("        column_values {");
        builder.AppendLine("            id");
        builder.AppendLine("            value");
        builder.AppendLine("            text");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    public string BuildGetBoardColumnsQuery(string boardId)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("    boards(ids: [").Append(boardId).AppendLine("]) {");
        builder.AppendLine("        columns {");
        builder.AppendLine("            id");
        builder.AppendLine("            title");
        builder.AppendLine("            type");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.Append('}');
        return builder.ToString();
    }

    private static string BuildQueryParamsFragment(MondayQueryParams queryParams)
    {
        var builder = new StringBuilder();
        builder.Append("{ rules: [");

        for (var index = 0; index < queryParams.Rules.Count; index++)
        {
            var rule = queryParams.Rules[index];
            builder.Append("{ ");
            builder.Append($"column_id: \"{EscapeGraphQlString(rule.ColumnId)}\", ");
            builder.Append($"operator: {rule.Operator}");

            if (rule.RequiresCompareValue && !string.IsNullOrWhiteSpace(rule.CompareValue))
            {
                builder.Append($", compare_value: \"{EscapeGraphQlString(rule.CompareValue!)}\"");
            }

            builder.Append(" }");
            if (index < queryParams.Rules.Count - 1)
            {
                builder.Append(", ");
            }
        }

        builder.Append("] }");
        return builder.ToString();
    }

    private static string EscapeGraphQlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}

