
# ProductionFlow

ProductionFlow is a full-stack web application for production
order management.

The project demonstrates business application development using
C#, ASP.NET Core, React, TypeScript and PostgreSQL.

It includes authentication, role-based authorization, production
workflows, execution reporting and automated testing.

## Technologies

### Backend
- C# / .NET 10
- ASP.NET Core REST API
- Entity Framework Core
- PostgreSQL 18
- ASP.NET Core Identity
- Cookie authentication and CSRF protection

### Frontend
- React
- TypeScript
- Vite
- React Router
- HTML and CSS

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

- Admin — administration and full production workflow
- Planner — production order planning and release
- Operator — execution reporting for assigned work centers

### Production Order Management

- Create production orders
- Select products and work centers
- Search, filter, sort and paginate orders
- View order details
- Track order change history

### Production Workflow

Draft -> Released -> InProgress -> Completed

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
- Order history

### Automated Testing

- Unit tests using xUnit
- Integration tests using Testcontainers
- End-to-end browser tests using Playwright

## Quick Start — Docker Compose

### Requirements

- Docker Desktop
- Docker Compose

### 1. Configure environment variables

Create a `.env` file in the repository root based on
`.env.example`.

Set PostgreSQL credentials and passwords for the three
demonstration users.

Do not commit `.env`.

### 2. Build and start the application

Run from the repository root:

```powershell
docker compose up --build -d
```

Docker Compose starts:

1. PostgreSQL
2. ASP.NET Core API
3. React frontend served by nginx

Database migrations and initial demonstration data are prepared
automatically in the local Docker development environment.

Wait until all three services are running and healthy.

### 3. Open ProductionFlow

Frontend:

http://localhost:3000

API:

http://localhost:8080

API health check:

http://localhost:8080/health

PostgreSQL is exposed locally on port 54330.

### 4. Demonstration accounts

| Role | Email |
|---|---|
| Admin | admin@productionflow.local |
| Planner | planner@productionflow.local |
| Operator | operator@productionflow.local |

Passwords are configured in the local `.env` file.

The Operator account requires a work center assignment
to perform operations on specific production orders.

### 5. Stop the application

```powershell
docker compose down
```

This preserves the PostgreSQL named volume.

Warning: `docker compose down -v` removes database volumes
and permanently deletes locally stored data.

## Local Development

The application can also run without Docker for the API
and frontend.

### PostgreSQL

From the repository root:

```powershell
docker compose up -d postgres
```

### Backend

Configure the connection string and demonstration passwords
in the ASP.NET Core User Secrets.

Start `ProductionFlow.Api` in Visual Studio using
the `http` profile.

API address:

http://localhost:5041

### Frontend

Run from the `frontend` directory:

```powershell
npm ci
npm run dev
```

Vite normally starts at:

http://localhost:5173

The development proxy forwards `/api` requests
to the backend.

## Tests

### .NET Tests

Run from the repository root:

```powershell
dotnet test ProductionFlow.sln
```

Integration tests require Docker Desktop.

### Frontend Checks

Run from `frontend`:

```powershell
npm run build
npm run lint
```

### Playwright

Configure local demonstration account passwords in:

`frontend/.env.e2e.local`

Install Chromium:

```powershell
npx playwright install chromium
```

Run tests against the development environment:

```powershell
npx playwright test
```

For Docker Compose, set `PF_E2E_TARGET=docker`
before running the tests.

Playwright tests create real demonstration orders in PostgreSQL.

## Project Structure

```text
ProductionFlow/
├── src/
│   └── ProductionFlow.Api/
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
- GitHub Actions for .NET
- React and TypeScript frontend
- Playwright E2E tests
- Docker Compose for the full application

Planned:

- Structured logging with Serilog
- Real-time updates using SignalR
- RabbitMQ and background processing
- Outbox pattern
- CSV reports
- Performance improvements
- Final portfolio presentation

## Security Notice

The Docker Compose configuration is intended for local
demonstration and learning.

It uses HTTP and development accounts.

Before public deployment, production configuration,
HTTPS, secrets management and additional security
hardening are required.
