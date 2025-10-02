User Story & Technical Specification: Monday.com Connector Write Operations

    ID: M2.T1

    Service: DataWorkflows.Connector.Monday

    Milestone: 2 (Fast Follow - Write & Workflow)

Part 1: User Story (Defined by Product)

As the Workflow Engine, I want to update columns (like links, timelines, and statuses) for specific items or subitems on a Monday.com board, so that I can keep the data aligned with other systems.
Part 2: Technical Specification (Defined by Architect)
General Acceptance Criteria (Apply to all endpoints)

    Given valid authentication and a valid request body, when a write operation is performed, then the service must respond with HTTP 200 OK and a full MondayItemDto representation of the updated item.

    Given an invalid request body (e.g., malformed JSON for a column value), when a write operation is attempted, then the service must respond with HTTP 400 Bad Request and a descriptive error message.

    Given the user attempts to update a non-existent item or column, when the operation is performed, then the service must respond with HTTP 404 Not Found.

    Architectural Note on Idempotency: Write operations are not required to be idempotent for the initial implementation. Duplicate requests will be processed as new, distinct operations.

Feature 1: Update an Item's Column Value

User Stories Covered: All write-based user stories.
Technical Requirements

    API Layer:

        Controller: ItemsController

        Method: UpdateItemColumnValueAsync

        Route: PATCH /api/v1/items/{itemId}/columns/{columnId}

        Signature: UpdateItemColumnValueAsync(string itemId, string columnId, [FromBody] UpdateColumnValueRequest request)

        Note on Sub-items: This endpoint handles both parent items and sub-items. The itemId route parameter should be the unique ID of the target item, regardless of its level.

    Application Layer:

        Define a new UpdateColumnValueRequest record with one field: string ValueJson.

        Add to IMondayApiClient: Task<MondayItemDto> UpdateColumnValueAsync(string itemId, string columnId, string valueJson, CancellationToken cancellationToken).

        Implement the MediatR command and handler for this operation.

    Infrastructure Layer:

        Implement UpdateColumnValueAsync in MondayApiClient.

        This method must construct and execute a parameterized GraphQL mutation (change_column_value).

        The itemId, columnId, and valueJson will be passed as variables.

        The mutation will return the complete item, which is mapped to a MondayItemDto.

Example JSON Payloads

    To update a Status column: { "ValueJson": "{ \"label\": \"Done\" }" }

    To update a Link column: { "ValueJson": "{ \"url\": \"https://example.com\", \"text\": \"Confluence Page\" }" }

    To update a Timeline column: { "ValueJson": "{ \"from\": \"2025-10-26\", \"to\": \"2025-10-28\" }" }