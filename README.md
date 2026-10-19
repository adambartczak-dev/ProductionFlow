
# ProductionFlow

ProductionFlow is a full-stack web application for production order management.

The project demonstrates business application development, production workflows, asynchronous messaging, reliable event processing and automated testing.

## Technologies

### Backend

- C# / .NET 10
- ASP.NET Core REST API
- Entity Framework Core
- PostgreSQL 18
- ASP.NET Core Identity
- Cookie authentication and CSRF protection
- SignalR
- Serilog

### Frontend

- React
- TypeScript
- Vite
- React Router
- HTML and CSS

### Messaging

- RabbitMQ 4.3
- Worker Service
- Transactional Outbox
- Publisher Confirms
- Idempotent Consumer
- Retry Queue
- Dead Letter Queue

### Testing and Infrastructure

- xUnit
- Testcontainers
- Playwright
- GitHub Actions
- Docker Compose
- nginx

## Features

### Authentication and Authorization

Three user roles are available:

- Admin — administration and production workflows
- Planner — production order planning and release
- Operator — execution reporting for assigned work centers

### Production Order Management

- Create and edit production orders
- Select products and work centers
- Search, filter, sort and paginate orders
- View order details and history
- Track production progress
- Receive real-time updates using SignalR

### Production Workflow

Draft → Released → InProgress → Completed

Orders can also be cancelled from Draft or Released.

Invalid operations are rejected by the backend.

### Execution Reporting

- Record good and scrap quantities
- Calculate production progress
- Prevent quantities exceeding the production plan
- Complete fully reported orders

### Data Consistency

- PostgreSQL transactions
- Row-level locking
- Business validation
- Conflict responses (HTTP 409)
- Order change history

### Reliable Messaging

ProductionFlow uses RabbitMQ to process production order events asynchronously.

The processing flow:

1. The API saves a production order change.
2. Transactional Outbox stores the event in PostgreSQL.
3. OutboxPublisher reads pending events.
4. Publisher Confirms acknowledge publication to RabbitMQ.
5. Worker receives events and processes them.
6. ProcessedMessages identifies duplicate EventIds.
7. Worker updates production summaries in PostgreSQL.

The summary update and EventId registration use one database transaction.

Retry Queue provides delayed retries for configured processing failures.

Dead Letter Queue stores messages that could not be processed successfully.

The system uses at-least-once messaging semantics. Duplicate deliveries are possible and handled using idempotent processing.

### Production Summaries

Worker maintains OrderProductionSummaries based on source production reports.

Summaries include:

- Planned quantity
- Good quantity
- Scrap quantity
- Number of execution reports
- Current production order status

The API provides:

- GET /api/order-summaries
- GET /api/order-summaries/export.csv

Summary endpoints require Admin or Planner authorization.

Summaries are eventually consistent and may be updated shortly after the original operation.

### Automated Testing

- Unit tests using xUnit
- Integration tests using Testcontainers and PostgreSQL
- Concurrent processing and duplicate message tests
- End-to-end browser tests using Playwright

## Architecture

```text
React + TypeScript
        |
        v
ASP.NET Core REST API
        |
        v
    PostgreSQL
        |
        v
   OutboxMessages
        |
        v
  OutboxPublisher
        |
        v
      RabbitMQ
        |
        v
   Worker Service
        |
        v
    PostgreSQL
        |
        +-- ProcessedMessages
        |
        +-- OrderProductionSummaries
```

SignalR provides real-time notifications to connected browser clients.

## Quick Start — Docker Compose

### Requirements

- Docker Desktop
- Docker Compose

### 1. Configure environment variables

Create a `.env` file in the repository root based on `.env.example`.

Configure:

- PostgreSQL credentials
- Demonstration user passwords
- RabbitMQ credentials

Do not commit `.env`.

### 2. Build and start the application

Run from the repository root:

```powershell
docker compose up -d --build
```

Docker Compose starts six services:

1. postgres
2. api
3. frontend
4. rabbitmq
5. worker
6. outbox-publisher

Database migrations and demonstration data are prepared automatically in the local Development environment.

Check the services:

```powershell
docker compose ps
```

### 3. Open ProductionFlow

Frontend:

http://localhost:3000

API:

http://localhost:8080

API health check:

http://localhost:8080/health

RabbitMQ Management:

http://localhost:15672

PostgreSQL is exposed locally on port 54330.

### 4. Demonstration accounts

| Role | Email |
|---|---|
| Admin | admin@productionflow.local |
| Planner | planner@productionflow.local |
| Operator | operator@productionflow.local |

Passwords are configured in the local `.env` file.

The Operator account requires a work center assignment for relevant production operations.

### 5. Messaging demonstration

Publish a normal demonstration event:

```powershell
docker compose run --rm publisher DemoOrderChanged
```

Test a transient processing failure:

```powershell
docker compose run --rm publisher DemoTransientFailure
```

Test a permanent processing failure:

```powershell
docker compose run --rm publisher DemoPermanentFailure
```

Test invalid JSON handling:

```powershell
docker compose run --rm publisher DemoInvalidJson
```

Inspect Worker logs:

```powershell
docker compose logs worker --tail 80
```

Inspect OutboxPublisher logs:

```powershell
docker compose logs outbox-publisher --tail 80
```

Use RabbitMQ Management to inspect the main queue, Retry Queue and Dead Letter Queue.

### 6. Stop the application

```powershell
docker compose down
```

This preserves PostgreSQL and RabbitMQ volumes.

Warning: `docker compose down -v` removes named volumes and their stored data.

## Tests

### .NET Tests

Run from the repository root:

```powershell
dotnet test ProductionFlow.sln
```

Integration tests require Docker Desktop.

### Frontend Checks

Run from the frontend directory:

```powershell
npm run lint
npm run build
```

### Playwright

Configure local demonstration account passwords in:

`frontend/.env.e2e.local`

Install Chromium if necessary:

```powershell
npx playwright install chromium
```

For Docker Compose testing, use PowerShell:

```powershell
$env:PF_E2E_TARGET = "docker"
npx playwright test
Remove-Item Env:PF_E2E_TARGET
```

Playwright tests create real demonstration orders in PostgreSQL.

## Project Structure

```text
ProductionFlow/
├── src/
│   ├── ProductionFlow.Api/
│   ├── ProductionFlow.Messaging/
│   ├── ProductionFlow.Publisher/
│   ├── ProductionFlow.Worker/
│   └── ProductionFlow.OutboxPublisher/
├── tests/
│   ├── ProductionFlow.UnitTests/
│   └── ProductionFlow.IntegrationTests/
├── frontend/
│   ├── src/
│   ├── e2e/
│   ├── Dockerfile
│   └── nginx.conf
├── docs/
├── compose.yaml
└── README.md
```

## Development Roadmap

Completed:

- Backend and PostgreSQL
- Authentication and authorization
- Unit and integration tests
- React and TypeScript frontend
- Playwright E2E tests
- Docker Compose
- Serilog and SignalR
- RabbitMQ and background processing
- Transactional Outbox
- Publisher Confirms
- Idempotent Consumer
- Retry and Dead Letter Queue
- Production summaries
- CSV export

Next:

- Extend CI and Docker validation
- Performance measurements and query optimization
- Final portfolio documentation and screenshots
- Fresh-clone verification
- Technical interview preparation

## Security and Reliability Notice

The Docker Compose configuration is intended for local development and demonstration.

It uses HTTP and development accounts.

The messaging implementation demonstrates important reliability patterns but is not a complete highly available production messaging platform.

Further production hardening would include secure deployment, TLS, secret management, monitoring, advanced failure recovery and appropriate RabbitMQ queue durability strategies.
