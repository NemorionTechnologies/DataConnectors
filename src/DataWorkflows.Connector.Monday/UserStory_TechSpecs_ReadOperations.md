User Story & Technical Specification: Monday.com Connector Read Operations

    ID: M1.T3

    Service: DataWorkflows.Connector.Monday

    Milestone: 1 (MVP - Read-Only)

Part 1: User Story (Defined by Product)

As the Workflow Engine, I want to retrieve various lists and details of items, sub-items, and updates from a Monday.com board via API calls, so that I can use that data as the starting point for automated workflows.

(Specific user stories have been consolidated for brevity in this final spec).
Part 2: Technical Specification (Defined by Architect)
Core Architectural Principles

    MediatR Pattern: All API endpoints will use the MediatR pattern. Controllers will be "thin," responsible only for creating a Query object, sending it via MediatR, and returning the result. All business logic resides in MediatR handlers within the Application Layer.

    Correlation IDs: A custom ASP.NET Core middleware will manage Correlation IDs. It will check for an X-Correlation-ID header; if one is not present, it will generate a new GUID. This ID will be attached to the logging context for all logs generated during the request's lifecycle.

    Resilience (Polly): All outgoing HTTP calls to the Monday.com API within the MondayApiClient (Infrastructure Layer) will be protected by a Polly retry policy, configured for exponential backoff to handle transient network failures.

General Acceptance Criteria (Apply to all endpoints)

    Given an invalid Monday.com API key, when any request is made, then the service must respond with HTTP 401 Unauthorized.

    Given the external Monday.com API is down or returns a server error, when any request is made, then the Polly policy will retry, and if it ultimately fails, the service must respond with HTTP 502 Bad Gateway.

    Given a resource (board, item) does not exist, when it is requested, then the MondayApiClient will inspect the errors field of the GraphQL response. If a "not found" error is present, a custom ResourceNotFoundException will be thrown, which a global exception handler will translate into an HTTP 404 Not Found response.

    Given any request, when it is processed, then a structured log with the Correlation ID, path, status, and duration must be recorded.

Data Transfer Objects (DTOs)

    GetItemsFilterModel: Used for querying items and sub-items.

    public class GetItemsFilterModel
    {
        public string? GroupId { get; set; }
        public DateRangeFilter? TimelineFilter { get; set; }
        public Dictionary<string, string>? ColumnFilters { get; set; } // Key: ColumnName, Value: ColumnValue
    }
    public class DateRangeFilter { public DateTime From { get; set; } public DateTime To { get; set; } }

    MondayItemDto: string Id, string ParentId, string Title, string GroupId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Dictionary<string, object> ColumnValues.

    MondayActivityLogDto: string EventType, string UserId, DateTimeOffset CreatedAt, string EventDataJson.

    MondayUpdateDto: string Id, string ItemId, string BodyText, string CreatorId, DateTimeOffset CreatedAt.

    MondayHydratedItemDto: Contains all fields from MondayItemDto plus IEnumerable<MondayItemDto> SubItems and IEnumerable<MondayUpdateDto> Updates.

Feature 1: Get Board Items

User Stories Covered: Get all items, get items by group, get items by column value, get items by date range.
Technical Requirements

    API Layer:

        Controller: BoardsController

        Method: GetItemsByBoardIdAsync

        Route: GET /api/v1/boards/{boardId}/items

        Signature: GetItemsByBoardIdAsync(string boardId, [FromQuery] GetItemsFilterModel filter)

    Application Layer:

        Add to IMondayApiClient interface: Task<IEnumerable<MondayItemDto>> GetBoardItemsAsync(string boardId, GetItemsFilterModel filter, CancellationToken cancellationToken).

        Implement the MediatR handler for this query.

    Infrastructure Layer:

        In MondayApiClient, implement the logic to utilize a parameterized GraphQL query, passing filter criteria as variables to efficiently fetch data from the Monday.com API.

Feature 2: Get Board Activity Log

User Stories Covered: Get full activity log, get activity log by time period.
Technical Requirements

    API Layer:

        Controller: BoardsController

        Method: GetBoardActivityLogAsync

        Route: GET /api/v1/boards/{boardId}/activity

        Signature: Accepts optional [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate.

    Application Layer:

        Add to IMondayApiClient: Task<IEnumerable<MondayActivityLogDto>> GetBoardActivityAsync(string boardId, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken).

        Implement the MediatR handler for this query.

    Infrastructure Layer:

        Implement the GetBoardActivityAsync method to query the activity_logs field via a parameterized GraphQL query.

Feature 3: Get Board Updates

User Stories Covered: Get all updates, get updates by time period.
Technical Requirements

    API Layer:

        Controller: BoardsController

        Method: GetBoardUpdatesAsync

        Route: GET /api/v1/boards/{boardId}/updates

        Signature: Accept optional [FromQuery] DateTime? fromDate and [FromQuery] DateTime? toDate.

    Application Layer:

        Add to IMondayApiClient: Task<IEnumerable<MondayUpdateDto>> GetBoardUpdatesAsync(string boardId, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken).

        Implement the MediatR handler for this query.

        Note: This endpoint can be inefficient. Prefer the item-specific endpoint (Feature 5) where possible.

    Infrastructure Layer:

        Implement the GetBoardUpdatesAsync method to query the updates field via a parameterized GraphQL query.

Feature 4: Get Item Sub-Items

User Stories Covered: Get a filtered list of sub-items.
Technical Requirements

    API Layer:

        Controller: ItemsController (new)

        Method: GetSubItemsAsync

        Route: GET /api/v1/items/{itemId}/subitems

        Signature: GetSubItemsAsync(string itemId, [FromQuery] GetItemsFilterModel filter)

    Application Layer:

        Add to IMondayApiClient: Task<IEnumerable<MondayItemDto>> GetSubItemsAsync(string parentItemId, GetItemsFilterModel filter, CancellationToken cancellationToken).

        Implement the MediatR handler for this query.

    Infrastructure Layer:

        Implement GetSubItemsAsync in MondayApiClient. The GraphQL query will fetch all sub-items for the parent item.

        Note: The Monday.com API does not support server-side filtering of sub-items. The filtering logic (based on the filter model) must be applied in-memory within this method after the data is retrieved.

Feature 5: Get Item Updates

User Stories Covered: Get updates for sub-items.
Technical Requirements

    API Layer:

        Controller: ItemsController

        Method: GetItemUpdatesAsync

        Route: GET /api/v1/items/{itemId}/updates

        Signature: GetItemUpdatesAsync(string itemId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)

    Application Layer:

        Add to IMondayApiClient: Task<IEnumerable<MondayUpdateDto>> GetItemUpdatesAsync(string itemId, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken).

        Implement the MediatR handler for this query.

    Infrastructure Layer:

        Implement GetItemUpdatesAsync in MondayApiClient to query the updates for a specific item ID via a parameterized GraphQL query. This is the preferred, high-performance method for getting updates.

Feature 6: Get Hydrated Board Items

User Stories Covered: Get items, their sub-items, and their updates in a single call.
Technical Requirements

    API Layer:

        Controller: BoardsController

        Method: GetHydratedItemsByBoardIdAsync

        Route: GET /api/v1/boards/{boardId}/hydrated-items

        Signature: GetHydratedItemsByBoardIdAsync(string boardId, [FromQuery] GetItemsFilterModel filter)

    Application Layer:

        Add to IMondayApiClient: Task<IEnumerable<MondayHydratedItemDto>> GetHydratedBoardItemsAsync(string boardId, GetItemsFilterModel filter, CancellationToken cancellationToken).

        Implement the MediatR handler for this query.

    Infrastructure Layer:

        Implement GetHydratedBoardItemsAsync in MondayApiClient. This method will orchestrate multiple calls:

            First, call the existing logic for GetBoardItemsAsync to get the list of top-level parent items based on the filter.

            Then, for each retrieved parent item, concurrently call the GetSubItemsAsync and GetItemUpdatesAsync methods (e.g., using Task.WhenAll).

            Finally, assemble the results into the MondayHydratedItemDto objects before returning. This approach reuses existing methods and avoids an overly complex single GraphQL query.