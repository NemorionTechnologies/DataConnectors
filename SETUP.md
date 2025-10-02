# DataWorkflows - Setup Guide

This guide will help you get the DataWorkflows system up and running.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed and running
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed (for local development)
- Git

## Quick Start

### 1. Clone and Configure

```bash
# Clone the repository (if applicable)
cd H:\Development\DataConnectors

# Create your .env file from the example
cp .env.example .env

# Edit .env with your actual API keys and credentials
# For MVP testing, you can leave the placeholder values - they won't be used yet
```

### 2. Build and Run with Docker Compose

```bash
# Build all services
docker-compose build

# Start all services
docker-compose up
```

This will start:
- PostgreSQL database (port 5433 - note: not 5432 to avoid conflicts)
- Workflow Engine (port 5001)
- Slack Bot (port 5002)
- Slack Connector (port 5010)
- Confluence Connector (port 5011)
- Monday Connector (port 5012)
- Outlook Connector (port 5013)
- TaskTracker Connector (port 5014)
- TaskTracker Mock API (port 5020)

### 3. Verify Services are Running

#### Health Checks (Liveness)
These endpoints check if the service is running:

- Workflow Engine: http://localhost:5001/api/v1/health/live
- TaskTracker Mock API: http://localhost:5020/api/health/live

#### Readiness Checks
These endpoints check if the service can accept traffic (all dependencies are available):

- Workflow Engine: http://localhost:5001/api/v1/health/ready
- TaskTracker Mock API: http://localhost:5020/api/health/ready

### 4. Explore Swagger UI

Each service has a Swagger UI for testing:

- Workflow Engine: http://localhost:5001/swagger
- Slack Bot: http://localhost:5002/swagger
- Slack Connector: http://localhost:5010/swagger
- Confluence Connector: http://localhost:5011/swagger
- Monday Connector: http://localhost:5012/swagger
- Outlook Connector: http://localhost:5013/swagger
- TaskTracker Connector: http://localhost:5014/swagger
- TaskTracker Mock API: http://localhost:5020/swagger

## Testing the TaskTracker Mock API

The TaskTracker Mock API simulates a proprietary task tracking system with OAuth-style authentication.

### 1. Login to get a token

```bash
curl -X POST http://localhost:5020/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser", "password": "testpass"}'
```

Response:
```json
{
  "token": "mock-token-12345678-1234-1234-1234-123456789abc",
  "expiresAt": "2025-10-02T12:00:00Z"
}
```

### 2. Use the token to access protected endpoints

```bash
# Get all tasks
curl -X GET http://localhost:5020/api/tasks \
  -H "Authorization: Bearer mock-token-12345678-1234-1234-1234-123456789abc"

# Create a task
curl -X POST http://localhost:5020/api/tasks \
  -H "Authorization: Bearer mock-token-12345678-1234-1234-1234-123456789abc" \
  -H "Content-Type: application/json" \
  -d '{"title": "New Task", "description": "Task description", "status": "New"}'
```

## Local Development

To run services locally outside of Docker:

```bash
# Restore all projects
dotnet restore

# Run the Workflow Engine
cd src/DataWorkflows.Engine
dotnet run

# Run the TaskTracker Mock API (in another terminal)
cd src/DataWorkflows.TaskTracker.MockApi
dotnet run
```

## Database Access

To connect to the PostgreSQL database:

```
Host: localhost
Port: 5433
Database: dataworkflows
Username: postgres
Password: [from your .env file]
```

Or use psql:
```bash
docker exec -it dataworkflows-postgres psql -U postgres -d dataworkflows
```

## Stopping Services

```bash
# Stop all services
docker-compose down

# Stop and remove volumes (WARNING: deletes all data)
docker-compose down -v
```

## Troubleshooting

### Services won't start
- Ensure Docker Desktop is running
- Check that ports 5001-5014 and 5020 are not in use
- Run `docker-compose logs [service-name]` to see error logs

### Database connection errors
- Ensure PostgreSQL container is healthy: `docker ps`
- Check connection string in .env file
- Wait for database to fully initialize (can take 10-15 seconds)

### Build errors
- Ensure .NET 8 SDK is installed: `dotnet --version`
- Clear Docker build cache: `docker-compose build --no-cache`

## Next Steps

1. Review the [README.md](./README.md) for architecture details
2. Explore the Swagger UIs to understand each service's API
3. Start implementing real connector logic for Monday, Slack, etc.
4. Build your first end-to-end workflow

## Architecture Overview

```
┌─────────────┐
│  Slack Bot  │
└──────┬──────┘
       │
       ├──────────┐
       │          │
       v          v
┌──────────┐  ┌────────────────┐
│ Workflow │  │ Slack Connector│
│  Engine  │  └────────────────┘
└────┬─────┘
     │
     ├──────┬──────┬──────┬──────────┐
     │      │      │      │          │
     v      v      v      v          v
  ┌────┐ ┌────┐ ┌────┐ ┌─────┐  ┌────────┐
  │Mon │ │Conf│ │Out │ │Task │  │TaskMock│
  │day │ │lue │ │look│ │Track│  │  API   │
  └────┘ └────┘ └────┘ └─────┘  └────────┘
                          │
                          v
                     ┌──────────┐
                     │PostgreSQL│
                     └──────────┘
```

For questions or issues, refer to the main README.md or create an issue in the project repository.
