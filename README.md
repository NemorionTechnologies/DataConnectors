Project: Data Workflows

Version: 1.1
Date: October 1, 2025
1. Vision & Mission

Problem: Project planning, task management, team communication, and contextual documentation are fragmented across multiple, disconnected tools (e.g., Monday.com, Confluence, Slack, Outlook). This creates data silos, requires manual effort to correlate information, and makes comprehensive status reporting difficult and time-consuming.

Mission: To create a centralized service that aggregates data from various sources, automates cross-platform workflows, and provides a unified view of project status, thereby increasing efficiency and visibility for management.
2. Core Architectural Principles

The system will be built on a modern, scalable, and maintainable foundation using the following principles:

    Microservices Architecture: The system will be composed of small, independent, and loosely coupled services. Each service will have a single responsibility, such as connecting to an external API or orchestrating a workflow. This allows for independent development, deployment, and scaling.

    Containerization: All services will be containerized using Docker. This ensures consistency across development, testing, and production environments and simplifies deployment orchestration. docker-compose will be used for local development environments.

    Clean/Hexagonal Architecture (Per Service): Each microservice will be a self-contained system implementing Clean/Hexagonal Architecture. This isolates the core business logic from external concerns like databases, APIs, and SDKs, making each service highly testable and maintainable. Crucially, there will be no shared core or domain logic between services to prevent creating a distributed monolith.

    API-Driven Communication: Services will communicate with each other via synchronous RESTful APIs for requests/commands. This provides a simple, well-understood, and decoupled method of integration.

    Observability & Analytics: The system will be designed for high observability. All workflow executions, steps, successes, failures, and timings will be logged in a structured format suitable for analytics. Services will expose telemetry data for monitoring and performance analysis.

    Resilience & Retry Policies: Each Connector Service will implement retry logic with exponential backoff for transient failures when communicating with external APIs. This ensures the system is resilient to temporary network issues or rate limiting.

    Testing Strategy: All services will include comprehensive unit tests for business logic and integration tests for API endpoints and external service interactions. Integration tests will verify contracts between services.

3. System Components
3.1. Workflow Engine

    Technology: ASP.NET Core 8 Web API

    MVP Approach: Single monolithic application containing API layer, orchestration logic, and database access. Will be decomposed into separate services (API Gateway, Workflow Service, Background Workers, Web UI) in future iterations as scale and complexity demands increase.

    Responsibilities:

        The central brain of the application.

        Manages the definition and execution of multi-step workflows.

        Orchestrates calls to the various Connector Services.

        Handles workflow state and provides detailed, structured logging of all executions (including timings, inputs, outputs, and errors) to the database for auditing and analytics purposes.

        Exposes a REST API for triggers to initiate workflows.

        Includes Swagger UI for manual testing and API exploration during development.

    Potential Libraries: Hangfire or Quartz.NET for background job processing (future).

3.2. Connector Services

    Technology: ASP.NET Core 8 Web API (one project per external service).

    Pattern: Each Connector is an Adapter that encapsulates all logic for interacting with a specific external API.

    Planned Connectors:
        - DataWorkflows.Connector.Slack (Slack API integration)
        - DataWorkflows.Connector.Confluence (Confluence API integration)
        - DataWorkflows.Connector.Monday (Monday.com API integration)
        - DataWorkflows.Connector.Outlook (Outlook/Microsoft Graph API integration)
        - DataWorkflows.Connector.TaskTracker (Proprietary Task Tracker - mock API for MVP)

    Responsibilities:

        Translation: Translates requests from the Workflow Engine into the specific format/SDK calls required by the external API.

        Standardization: Exposes a simple, standardized internal API for the Workflow Engine to consume.

        Authentication: Manages credentials for its specific external service.

3.3. Slack Bot

    Technology: ASP.NET Core 8 Web API (separate project from Slack Connector)

    Architecture: The Slack Bot is a dedicated service focused on user interaction. It communicates with the DataWorkflows.Connector.Slack service when it needs to perform Slack API operations (posting messages, reading channels, etc.).

    Responsibilities:

        Acts as a user-facing interface for the system.

        Listens for Slack slash commands (e.g., /status <project-name>) and @mentions (e.g., @OtherDoug give status...).

        Translates user commands into API calls to the Workflow Engine.

        Calls the Slack Connector to post formatted results back to the user in Slack.

3.4. Shared Database

    Technology: PostgreSQL with Dapper

    Data Access Strategy: All data access will be performed via Stored Procedures. Dapper will be used as a high-performance micro-ORM to execute the stored procedures and map their results to C# objects. The Workflow Engine is the primary consumer of the database. Connector Services are stateless and do not directly access the database except for authentication validation if required.

    Responsibilities:

        Stores workflow definitions.

        Logs detailed workflow execution telemetry in a structured format, including step-level timings, success/failure status, and payload information for future analytics.

        Persists any state required for long-running or complex workflows.

3.5. Proprietary Task Tracker (Mock Service)

    Technology: ASP.NET Core 8 Web API

    Purpose: Mock API simulating a proprietary internal task tracking system for MVP development and testing.

    Authentication Flow:
        - POST /api/auth/login - Accepts username/password, returns a mock JWT token
        - All other endpoints require Authorization: Bearer {token} header (OAuth 2.0 style)
        - Token validation is simplified for MVP (accepts any non-empty token generated by login endpoint)

    Mock Endpoints:
        - POST /api/auth/login - Login and receive token
        - GET /api/tasks - List all tasks
        - GET /api/tasks/{id} - Get task by ID
        - POST /api/tasks - Create new task
        - PUT /api/tasks/{id} - Update task
        - DELETE /api/tasks/{id} - Delete task

    Implementation: In-memory data storage, basic CRUD operations, simple JWT generation without actual cryptographic validation (for MVP only).

3.6. Configuration & Secrets Management

    Development: Secrets (API keys, connection strings) will be stored in a .env file (excluded from version control via .gitignore). A .env.example file with placeholder values (e.g., "YOUR_API_KEY_HERE") will be checked into the repository.

    Production: Future production deployments will integrate with enterprise secret management solutions (e.g., Azure Key Vault, HashiCorp Vault, AWS Secrets Manager) when the project matures.

3.7. Schema Evolution Strategy

    Status: Under consideration for future implementation. Given the MVP scope and single-developer context, formal schema versioning is not an immediate priority. This will be revisited as the system matures and requires more rigorous change management.

4. Phased Development Roadmap
Milestone 1: MVP - Foundational Read-Only Connector

    Goal: Prove the core architecture and establish connectivity with a single data source.

    Tasks:

        Solution Setup: Create a .NET 8 solution with a Git repository and a root docker-compose.yml file.

        Define Common Contracts: Create a lightweight shared project for common DTOs used for inter-service communication (e.g., ConnectorItemDto).

        Build Monday.com Connector: Create the DataWorkflows.Connector.Monday API project. Implement read-only functionality (GET /items/{boardId}) with retry logic and exponential backoff using Polly. Add unit and integration tests. Add a Dockerfile.

        Build Initial Workflow Engine: Create the DataWorkflows.Engine API project. Implement a simple test endpoint that calls the Monday.com Connector via HTTP. Add unit and integration tests. Add a Dockerfile.

        Database Setup: Create PostgreSQL database with initial schema for workflow definitions and execution logs.

        End-to-End Test: Configure docker-compose to run both services and PostgreSQL. Manually trigger the engine's test endpoint and verify that data from Monday.com is successfully retrieved and logged to the database.

Milestone 2: Fast Follow - Write Capabilities & First Workflow

    Goal: Implement the first end-to-end automated workflow involving writing data.

    High-Level Tasks: Build the Connector for the Proprietary Task Tracker. Implement a workflow in the Engine that reads from Monday.com and creates corresponding items in the task tracker.

Milestone 3: Fast Follow - Slack Bot Integration

    Goal: Provide a user-friendly way to trigger workflows.

    High-Level Tasks: Build the Slack Bot service. Implement a slash command and @mention handler that can trigger the workflow from Milestone 2.

Milestone 4: Fast Follow - Templating & Reporting

    Goal: Format aggregated data into human-readable reports.

    High-Level Tasks: Integrate a templating engine (e.g., Scriban). Create a workflow that gathers data from multiple sources and uses a template to generate a status report message for Slack.

5. Technology Stack Summary

    Language/Framework: C# / .NET 8, ASP.NET Core 8

    Database: PostgreSQL

    Data Access: Dapper & Stored Procedures

    Resilience: Polly for retry policies and circuit breakers

    Testing: xUnit for unit tests, integration tests for API contracts

    Containerization: Docker, Docker Compose

    Architecture: Microservices, Clean/Hexagonal Architecture

    Secrets Management: .env files (dev), enterprise vault solutions (future production)

6. Scale & Performance Targets

    Target Capacity: Support up to 1,000 concurrent workflow executions

    Future Features: Conditional logic, nested AND/OR conditions, logical gates, branching workflows (post-MVP)
