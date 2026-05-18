# SampleMicroservice

A standalone microservice that mirrors the TOA API monolith's Clean Architecture, patterns, and conventions. Built to demonstrate how the same engineering team would extract a feature domain into an independent service.

---

## Architecture

This service replicates the TOA API monolith's layer structure exactly:

```
SampleMicroservice/
├── src/
│   ├── Core/
│   │   ├── Domain/          — Entities, base classes, IAggregateRoot
│   │   └── Application/     — MediatR requests/handlers, validators, DTOs, interfaces
│   ├── Infrastructure/      — EF Core DbContext, repositories, middleware, auth
│   └── Host/                — ASP.NET Core entry point, controllers, config loading
├── Dockerfile
└── README.md
```

### How it matches the monolith

| Concern | Monolith | This Microservice |
|---|---|---|
| Layer structure | Domain / Application / Infrastructure / Host | Identical |
| Namespace root | `Toa.Api.*` | `SampleMicroservice.*` |
| Request pattern | `IRequest<ApiResponse<T>>` | Identical |
| Handler pattern | `IRequestHandler<TRequest, ApiResponse<T>>` | Identical |
| Validation pipeline | `CustomValidator<T>` + `ValidationBehavior<,>` | Identical |
| Response wrapper | `ApiResponse<T> { Message, Data }` | Identical |
| Error response | `ErrorResult { Messages, StatusCode, ErrorId, ... }` | Identical |
| Exception hierarchy | `CustomException` → `NotFoundException`, `ConflictException` | Identical |
| Repository pattern | `IRepository<T>` / `IReadRepository<T>` (Ardalis) | Identical |
| Entity base | `AuditableEntity<TId>` + `IAggregateRoot` | Identical |
| Controller base | `VersionedApiController` → `BaseApiController` | Identical |
| DI registration | Extension methods on `IServiceCollection` | Identical |
| Auto-service scan | `IScopedService` / `ITransientService` marker interfaces | Identical |
| Config loading | JSON files in `/Configurations` folder | Identical |
| Logging | Serilog with context enrichment | Identical |
| Exception handling | `ExceptionMiddleware` (global, returns `ErrorResult`) | Identical |
| Database | EF Core 7 with Ardalis Specification pattern | Identical |
| API versioning | `api/v{version}/[controller]`, default v1.0 | Identical |
| OpenAPI | NSwag | Identical |

---

## Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/v1/sample-items` | List all sample items |
| POST | `/api/v1/sample-items` | Create a sample item |
| DELETE | `/api/v1/sample-items/{id}` | Delete a sample item by ID |

**Health check:** `GET /health`

**Swagger UI:** `GET /swagger`

### POST body

```json
{
  "name": "My Sample Item"
}
```

---

## How to Run

### Prerequisites

- .NET 7 SDK
- (Optional) SQL Server for persistent storage — defaults to in-memory if `ConnectionStrings:DefaultConnection` is empty

### Local

```bash
cd services/SampleMicroservice/src/Host
dotnet run
```

The service starts on `http://localhost:5000` / `https://localhost:5001` by default.

### Docker

```bash
cd services/SampleMicroservice
docker build -t sample-microservice .
docker run -p 8080:80 sample-microservice
```

### With SQL Server

Set the connection string via environment variable or `Configurations/database.json`:

```bash
docker run -p 8080:80 \
  -e ConnectionStrings__DefaultConnection="Server=...;Database=SampleMicroserviceDb;..." \
  sample-microservice
```

---

## Database

By default the service uses **EF Core In-Memory** database — no setup required, data is lost on restart. This is intended for development and demo purposes.

For persistent storage, set `ConnectionStrings:DefaultConnection` to a SQL Server connection string. The application calls `EnsureCreated()` on startup to provision the schema automatically.

The `SampleItem` table is placed in the `sample` schema to avoid collisions with other services or the monolith.

---

## Configuration

Configuration follows the same layered pattern as the monolith:

| File | Purpose |
|---|---|
| `appsettings.json` | Base config (Serilog levels, allowed hosts) |
| `appsettings.Development.json` | Development overrides |
| `Configurations/database.json` | DB provider and connection string |
| `Configurations/logger.json` | Logger settings (app name, sinks) |

All values can be overridden with environment variables using the standard `__` delimiter:

```
ConnectionStrings__DefaultConnection=Server=...
LoggerSettings__AppName=MyService
```

---

## Adding Authentication

The service ships with authentication disabled. To enable it, matching the monolith's JWT or Azure AD setup:

1. Register auth in `Infrastructure/Startup.cs` `AddInfrastructure`:
   ```csharp
   services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options => { ... });
   ```

2. Add `[Authorize]` to controllers or individual actions.

3. Add `SecuritySettings` config class and bind it from `Configurations/security.json`, following the same pattern as the monolith.

---

## APIM Registration

This service is APIM-ready. To register with Azure API Management:

1. Deploy the service and expose a public HTTPS endpoint.
2. Import the OpenAPI spec from `/swagger/v1/swagger.json` into APIM.
3. Set the backend URL to the service's base URL.
4. Apply the same JWT/Azure AD validation policy already used by the monolith's APIM instance — no changes to auth configuration are needed.

---

## Observability

- **Health check:** `/health` — returns HTTP 200 when the service is alive.
- **Structured logs:** Serilog with `ErrorId`, `UserId` context pushed per request/exception.
- **OpenTelemetry:** Not wired by default. Add `OpenTelemetry.Extensions.Hosting` and configure in `Infrastructure/Startup.cs` following the same approach as adding any other infrastructure concern.

---

## Project Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Ardalis.Specification` | 6.1.0 | Repository + Specification pattern |
| `MediatR.Extensions.Microsoft.DependencyInjection` | 11.1.0 | CQRS / mediator |
| `FluentValidation` | 11.5.2 | Validation pipeline |
| `Mapster` | 7.3.0 | Entity → DTO mapping |
| `NSwag.AspNetCore` | 13.18.2 | OpenAPI / Swagger UI |
| `Serilog.AspNetCore` | 6.1.0 | Structured logging |
| `Microsoft.EntityFrameworkCore` | 7.0.4 | ORM |
| `Microsoft.AspNetCore.Mvc.Versioning` | 5.0.0 | API versioning |
