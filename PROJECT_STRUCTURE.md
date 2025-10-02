# DataWorkflows - Project Structure

## Solution Overview

```
DataConnectors/
├── src/
│   ├── DataWorkflows.Contracts/              # Shared DTOs and contracts
│   │   ├── ConnectorItemDto.cs
│   │   └── ConnectorResponse.cs
│   │
│   ├── DataWorkflows.Engine/                 # Workflow orchestration engine
│   │   ├── Core/
│   │   │   ├── Domain/                       # Domain models
│   │   │   ├── Application/                  # Business logic
│   │   │   │   └── WorkflowOrchestrator.cs
│   │   │   └── Interfaces/
│   │   │       └── IWorkflowOrchestrator.cs
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/                  # Database access
│   │   │   └── Http/                         # HTTP clients
│   │   ├── Presentation/
│   │   │   └── Controllers/
│   │   │       └── WorkflowController.cs
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── DataWorkflows.SlackBot/               # Slack user interface
│   │   ├── Core/
│   │   │   ├── Application/
│   │   │   └── Interfaces/
│   │   ├── Infrastructure/
│   │   │   └── Http/
│   │   ├── Presentation/
│   │   │   └── Controllers/
│   │   │       └── SlackEventController.cs
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── DataWorkflows.Connector.Slack/        # Slack API adapter
│   │   ├── Controllers/
│   │   │   └── SlackConnectorController.cs
│   │   └── Dockerfile
│   │
│   ├── DataWorkflows.Connector.Confluence/   # Confluence API adapter
│   │   ├── Controllers/
│   │   │   └── ConfluenceConnectorController.cs
│   │   └── Dockerfile
│   │
│   ├── DataWorkflows.Connector.Monday/       # Monday.com API adapter
│   │   ├── Controllers/
│   │   │   └── MondayConnectorController.cs
│   │   └── Dockerfile
│   │
│   ├── DataWorkflows.Connector.Outlook/      # Microsoft Graph API adapter
│   │   ├── Controllers/
│   │   │   └── OutlookConnectorController.cs
│   │   └── Dockerfile
│   │
│   ├── DataWorkflows.Connector.TaskTracker/  # TaskTracker API adapter
│   │   ├── Controllers/
│   │   │   └── TaskTrackerConnectorController.cs
│   │   └── Dockerfile
│   │
│   └── DataWorkflows.TaskTracker.MockApi/    # Mock proprietary task tracker
│       ├── Controllers/
│       │   ├── AuthController.cs             # OAuth-style authentication
│       │   └── TasksController.cs            # CRUD operations
│       ├── Models/
│       │   └── Task.cs
│       ├── Services/
│       │   ├── AuthService.cs                # Token management
│       │   └── TaskStore.cs                  # In-memory storage
│       ├── Program.cs
│       └── Dockerfile
│
├── database/
│   └── init/
│       └── 01_init_schema.sql                # Database initialization
│
├── docker-compose.yml                         # Orchestrates all services
├── .env.example                               # Environment variable template
├── .gitignore
├── DataWorkflows.sln                          # Solution file
├── README.md                                  # Architecture documentation
├── SETUP.md                                   # Setup instructions
└── PROJECT_STRUCTURE.md                       # This file

## Service Ports

| Service                  | Port | Description                          |
|--------------------------|------|--------------------------------------|
| PostgreSQL               | 5432 | Database                             |
| Workflow Engine          | 5001 | Orchestration API                    |
| Slack Bot                | 5002 | User interface                       |
| Slack Connector          | 5010 | Slack API integration                |
| Confluence Connector     | 5011 | Confluence API integration           |
| Monday Connector         | 5012 | Monday.com API integration           |
| Outlook Connector        | 5013 | Microsoft Graph API integration      |
| TaskTracker Connector    | 5014 | TaskTracker API client               |
| TaskTracker Mock API     | 5020 | Mock proprietary task tracker        |

## Clean Architecture Layers

Each service follows Clean/Hexagonal Architecture:

### Core Layer (Business Logic)
- **Domain**: Entities and value objects
- **Application**: Use cases and business logic
- **Interfaces**: Abstractions (ports)

### Infrastructure Layer (External Concerns)
- **Persistence**: Database implementations
- **Http**: HTTP clients for external APIs

### Presentation Layer (Entry Points)
- **Controllers**: REST API endpoints

## Key Technologies

- **.NET 9**: Framework
- **ASP.NET Core**: Web APIs
- **Swagger/OpenAPI**: API documentation
- **Polly**: Resilience and retry policies
- **Dapper**: Data access
- **PostgreSQL**: Database
- **Docker & Docker Compose**: Containerization

## Development Workflow

1. **Make changes** to any service
2. **Rebuild** the affected service: `docker-compose build [service-name]`
3. **Restart** the service: `docker-compose up [service-name]`
4. **Test** using Swagger UI or curl

## Testing the System

### End-to-End Flow

1. **Login to TaskTracker Mock API** → Get token
2. **Call TaskTracker Connector** → Uses token to fetch tasks
3. **Call Workflow Engine** → Orchestrates multiple connector calls
4. **Call Slack Bot** → Triggers workflow, posts results to Slack

### Health Checks

All services expose `/health` endpoints for monitoring.

## Next Implementation Steps

### Milestone 1: MVP - Foundational Read-Only Connector
- [ ] Implement actual Monday.com API integration with Polly retry
- [ ] Add database persistence to Workflow Engine
- [ ] Create first workflow definition
- [ ] Add comprehensive unit tests
- [ ] Add integration tests

### Milestone 2: Write Capabilities & First Workflow
- [ ] Implement TaskTracker Connector → Mock API integration
- [ ] Create workflow: Monday → TaskTracker sync
- [ ] Add workflow execution logging

### Milestone 3: Slack Bot Integration
- [ ] Implement Slack slash command parsing
- [ ] Connect Slack Bot → Workflow Engine
- [ ] Add Slack Connector → Slack API integration

## File Naming Conventions

- **Controllers**: `[Entity]Controller.cs`
- **Services**: `[ServiceName]Service.cs`
- **Interfaces**: `I[InterfaceName].cs`
- **DTOs**: `[Entity]Dto.cs`
- **Requests**: `[Action][Entity]Request.cs`

## Database Tables

- **workflows**: Workflow definitions
- **workflow_executions**: Execution telemetry
- **workflow_execution_steps**: Step-level telemetry

## Environment Variables

See `.env.example` for all configuration options.

## Additional Resources

- [SETUP.md](./SETUP.md) - Complete setup instructions
- [README.md](./README.md) - Architecture and design decisions
