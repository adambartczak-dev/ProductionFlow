
# ProductionFlow

ProductionFlow is a production order management application built
with C# and ASP.NET Core.

The project demonstrates backend development, business rules,
PostgreSQL integration, REST API design and automated unit testing.

This repository is under active development.

## Current Technologies

- C# / .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL 18
- Docker Compose
- xUnit
- Git / GitHub

## Current Features

### Product and Work Center Management

- Create products and work centers
- Retrieve existing records
- Update product and work center details
- Validate input data
- Prevent duplicate codes

### Production Order Management

- Create production orders
- Assign products and work centers
- Validate planned quantities and due dates
- Filter and search orders
- Sort and paginate results

### Order Status Workflow

Draft -> Released -> InProgress -> Completed

Orders can also be cancelled from Draft or Released.

Invalid status transitions are rejected.

### Production Reporting

- Record good and scrap quantities
- Prevent reporting beyond planned quantity
- Calculate production progress
- Complete fully reported orders

### Data Consistency

- PostgreSQL transactions
- Row-level locking for concurrent operations
- Atomic saving of business data
- Order change history

### Error Handling

- HTTP 400 for invalid requests
- HTTP 404 for missing resources
- HTTP 409 for business conflicts
- ProblemDetails error responses

## Project Structure

- src/ProductionFlow.Api
  - Controllers
  - Data
  - Domain
  - Dtos
  - Learning
- tests/ProductionFlow.UnitTests
- docs/requirements.md
- compose.yaml

## Requirements

- .NET 10 SDK
- Docker Desktop
- Visual Studio 2022 with .NET development tools

## Local Setup

### 1. PostgreSQL

Create a `.env` file in the repository root based on `.env.example`.

Set:

- POSTGRES_DB
- POSTGRES_USER
- POSTGRES_PASSWORD

Start PostgreSQL:

```powershell
docker compose up -d
```

The database is exposed on localhost:54330.

### 2. Database Connection

Configure the connection string using .NET User Secrets
in the ProductionFlow.Api project.

Use the key:

`ConnectionStrings:ProductionFlow`

The connection string should contain:

- Host: localhost
- Port: 54330
- Database: value from POSTGRES_DB
- Username: value from POSTGRES_USER
- Password: value from POSTGRES_PASSWORD

Do not commit passwords or local `.env` files.

### 3. Database Migrations

Open Package Manager Console in Visual Studio.

Run:

```powershell
Update-Database -Project ProductionFlow.Api -StartupProject ProductionFlow.Api -Context ProductionFlowDbContext -Args '--environment Development'
```

### 4. Run the API

Set `ProductionFlow.Api` as the startup project.

Select the `http` launch profile and run the application.

Default API address:

`http://localhost:5041`

### 5. Test the API

Use the HTTP request files in:

`src/ProductionFlow.Api`

For the complete production workflow, use:

`BackendEndToEnd.http`

### 6. Run Unit Tests

From the repository root:

```powershell
dotnet test ProductionFlow.sln
```

## Main API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | /api/products | List products |
| POST | /api/products | Create product |
| PUT | /api/products/{id} | Update product |
| GET | /api/workcenters | List work centers |
| POST | /api/workcenters | Create work center |
| PUT | /api/workcenters/{id} | Update work center |
| GET | /api/orders | List and filter orders |
| POST | /api/orders | Create order |
| GET | /api/orders/{id} | Get order details |
| POST | /api/orders/{id}/release | Release order |
| POST | /api/orders/{id}/start | Start production |
| POST | /api/orders/{id}/cancel | Cancel order |
| POST | /api/orders/{id}/reports | Add execution report |
| GET | /api/orders/{id}/reports | List execution reports |
| GET | /api/orders/{id}/progress | Get production progress |
| GET | /api/orders/{id}/history | Get order history |
| POST | /api/orders/{id}/complete | Complete order |

## Development Status

The PostgreSQL and backend stage is complete.

Upcoming development:

- ASP.NET Core Identity and user roles
- Authentication and authorization
- Integration tests and CI
- React / TypeScript frontend
- SignalR
- RabbitMQ and background processing
- Full Docker deployment

The current API is intended for local development and learning.
Authentication and role-based access control have not yet
been implemented.
