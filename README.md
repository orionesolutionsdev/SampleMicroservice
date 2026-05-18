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


 ---
  1. Monolith ↔ Microservice Communication

  Two patterns depending on whether you need a response or not.

  Synchronous (need a response back)

  Client
    │
    ▼
  APIM  ──── routes by path prefix ────────────────────────┐
    │                                                        │
    ▼                                                        ▼
  Toa.Api (Monolith)                            SampleMicroservice
  /api/v1/projects/*                            /api/v1/sample-items/*
    │                                                        ▲
    │  monolith needs sample items?                         │
    └────── HttpClient (typed) ──────────────────────────────┘
            forwards X-Correlation-Id header
            (via CorrelationIdDelegatingHandler)

  The CorrelationIdDelegatingHandler is already built in the microservice at:
  src/Infrastructure/Http/CorrelationIdDelegatingHandler.cs

  In the monolith, register it the same way whenever calling the microservice:
  // Toa.Api — Infrastructure/Startup.cs
  services.AddTransient<CorrelationIdDelegatingHandler>();

  services.AddHttpClient<ISampleItemService, SampleItemService>(client =>
  {
      client.BaseAddress = new Uri(config["Services:SampleMicroservice:BaseUrl"]!);
  })
  .AddHttpMessageHandler<CorrelationIdDelegatingHandler>(); // forwards X-Correlation-Id

  // Toa.Api — Infrastructure/SampleService/SampleItemService.cs
  public class SampleItemService : ISampleItemService, ITransientService
  {
      private readonly HttpClient _client;
      public SampleItemService(HttpClient client) => _client = client;

      public Task<ApiResponse<List<SampleItemDto>>> GetSampleItemsAsync(CancellationToken ct = default)
          => _client.GetFromJsonAsync<ApiResponse<List<SampleItemDto>>>("api/v1/sample-items", ct)!;
  }

  Called from a MediatR handler exactly like any other service already in the monolith:
  // in any monolith handler
  public async Task<ApiResponse<...>> Handle(SomeRequest request, CancellationToken ct)
  {
      var items = await _sampleItemService.GetSampleItemsAsync(ct);
      ...
  }

  ---
  Asynchronous (fire and forget / event-driven)

  Use this when the monolith does not need to wait for the microservice to finish.

  Monolith publishes event to Azure Service Bus Topic
           │
           ▼
     [project-events topic]
           │
           ├──► subscription: sample-svc  ──► SampleMicroservice processes it
           └──► subscription: other-svc   ──► AnotherService processes it

  // Monolith — publishes after a project is created
  await _serviceBusClient.SendMessageAsync(new ServiceBusMessage(
      JsonSerializer.Serialize(new ProjectCreatedEvent(project.Id, project.Name)))
  {
      Subject       = "ProjectCreated",
      CorrelationId = correlationId   // carry the same CorrelationId into the bus
  });

  // SampleMicroservice — consumes via BackgroundService
  // src/Infrastructure/Messaging/ProjectCreatedConsumer.cs
  public class ProjectCreatedConsumer : BackgroundService, IScopedService
  {
      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          await foreach (var msg in _processor.ReceiveMessagesAsync(stoppingToken))
          {
              using (LogContext.PushProperty("CorrelationId", msg.CorrelationId))  // restore trace chain
              {
                  var evt = JsonSerializer.Deserialize<ProjectCreatedEvent>(msg.Body);
                  await _mediator.Send(new HandleProjectCreatedCommand(evt!));
              }
          }
      }
  }

  ▎ Key rule: Always carry CorrelationId through the Service Bus message CorrelationId property and restore it on the
  ▎ consumer side so logs stay linked across async boundaries.

  ---
  2. Microservice ↔ Microservice Communication

  Same two patterns. The only difference is there is no monolith involved.

  ┌─────────────────┐         ┌──────────────────┐        ┌──────────────────┐
  │ SampleMicroSvc  │──HTTP──▶│  BillingMicroSvc  │        │  NotifMicroSvc   │
  │                 │         └──────────────────┘        └──────────────────┘
  │                 │──────── Service Bus Topic ──────────────────────────────▶│
  └─────────────────┘         (async, no coupling)

  Sync — typed client with CorrelationId forwarding

  Every microservice registers CorrelationIdDelegatingHandler (already in SampleMicroservice) and attaches it to each
  named HttpClient:

  // ServiceA calling ServiceB
  services.AddHttpClient<IBillingService, BillingService>(client =>
      client.BaseAddress = new Uri(config["Services:BillingService:BaseUrl"]!))
  .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

  The CorrelationIdDelegatingHandler reads the X-Correlation-Id from the current request and injects it into the
  outbound request — automatically, with no handler code change required.

  Async — one publisher, many independent consumers

  ServiceA publishes "SampleItemCreated"
           │
      [sample-item-events topic]
           ├──► BillingService  (creates invoice)
           └──► AuditService    (records audit trail)

  ServiceA has NO knowledge of BillingService or AuditService.
  Adding a new consumer requires zero change to ServiceA.

  Decision guide

  ┌───────────────────────────────────────────┬───────────────────┐
  │                 Scenario                  │        Use        │
  ├───────────────────────────────────────────┼───────────────────┤
  │ Need the response to continue             │ Sync HTTP         │
  ├───────────────────────────────────────────┼───────────────────┤
  │ Triggering work that runs independently   │ Async Service Bus │
  ├───────────────────────────────────────────┼───────────────────┤
  │ Multiple services react to the same thing │ Service Bus Topic │
  ├───────────────────────────────────────────┼───────────────────┤
  │ High volume stream (telemetry, clicks)    │ Azure Event Hubs  │
  └───────────────────────────────────────────┴───────────────────┘

  ---
  3. Traces and Logs — What's Built vs What to Configure

  What's already running in SampleMicroservice

  CorrelationIdMiddleware — src/Infrastructure/Middleware/CorrelationIdMiddleware.cs

  Every inbound request:
  1. Reads X-Correlation-Id header (or generates a new one if absent)
  2. Pushes it into Serilog LogContext for every log in that request
  3. Echoes it back on the response header

  ServiceName + CorrelationId in every log line — src/Host/Program.cs

  .Enrich.WithProperty("ServiceName", serviceName)
  .WriteTo.Console(outputTemplate:
      "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [CID:{CorrelationId}] {Message:lj}{NewLine}{Exception}")

  OpenTelemetry — src/Infrastructure/Startup.cs

  services.AddOpenTelemetry()
      .ConfigureResource(r => r.AddService(serviceName))
      .WithTracing(tracing => tracing
          .AddAspNetCoreInstrumentation(...)
          .AddHttpClientInstrumentation()
          .AddOtlpExporter(...)   // activated only when endpoint is set
      );

  What your logs look like right now (no extra setup needed)

  [09:15:01 INF] [SampleMicroservice] [CID:f3a1-...]  Server booting up...
  [09:15:04 INF] [SampleMicroservice] [CID:req-abc-123]  Sample items retrieved successfully.
  [09:15:05 ERR] [SampleMicroservice] [CID:req-abc-123]  Request failed with Status Code 404 and Error Id err-xyz.

  Monolith logs (once you add ServiceName enrichment there too):
  [09:15:04 INF] [Toa.Api]            [CID:req-abc-123]  Project created.
  [09:15:04 INF] [Toa.Api]            [CID:req-abc-123]  Calling SampleMicroservice...
  [09:15:04 INF] [SampleMicroservice] [CID:req-abc-123]  Sample item created successfully.

  Same CID across both services. One search finds the full journey.

  How to identify a specific service's traces

  ┌─────────────────────────────────────────────────┬──────────────────────────────────────────────────┐
  │              What you want to find              │                    Search by                     │
  ├─────────────────────────────────────────────────┼──────────────────────────────────────────────────┤
  │ All logs from this microservice only            │ ServiceName = "SampleMicroservice"               │
  ├─────────────────────────────────────────────────┼──────────────────────────────────────────────────┤
  │ Everything for one user request (cross-service) │ CorrelationId = "req-abc-123"                    │
  ├─────────────────────────────────────────────────┼──────────────────────────────────────────────────┤
  │ Full distributed trace (parent/child spans)     │ TraceId = "4bf92f35..." (from OTEL)              │
  ├─────────────────────────────────────────────────┼──────────────────────────────────────────────────┤
  │ All errors across every service                 │ Level = ERROR                                    │
  ├─────────────────────────────────────────────────┼──────────────────────────────────────────────────┤
  │ One request's path across async Service Bus     │ CorrelationId (passed through msg.CorrelationId) │
  └─────────────────────────────────────────────────┴──────────────────────────────────────────────────┘

  To enable a centralized log backend (point both services at the same sink)

  // logger.json in SampleMicroservice (already exists)
  {
    "LoggerSettings": {
      "AppName": "SampleMicroservice",      ← unique per service
      "ElasticSearchUrl": "http://elk:9200" ← same URL in both services
    }
  }

  // logger.json in Toa.Api monolith
  {
    "LoggerSettings": {
      "AppName": "Toa.Api",                 ← unique per service
      "ElasticSearchUrl": "http://elk:9200" ← same URL
    }
  }

  Both services write to the same Elasticsearch index. Kibana searches by ServiceName or CorrelationId across both. No
  code change needed — only config.

  To enable distributed tracing (Jaeger / Grafana Tempo / Azure Monitor)

  // appsettings.json in SampleMicroservice (already has this key)
  {
    "OpenTelemetry": {
      "Endpoint": "http://your-otel-collector:4317"
    }
  }

  Set that one value and the OTLP exporter activates. The trace spans from both the monolith and SampleMicroservice will
   appear as a single tree in the tracing UI — linked by the W3C traceparent header that HttpClient forwards
  automatically.

  ---
  Complete flow end-to-end

  Browser/APIM
      │  X-Correlation-Id: req-abc-123
      │  traceparent: 00-traceId-spanId-01
      ▼
  Toa.Api Monolith
      log: [Toa.Api] [CID:req-abc-123] Handling CreateProject
      │  HttpClient forwards both headers downstream
      ▼
  SampleMicroservice
      log: [SampleMicroservice] [CID:req-abc-123] Sample item created
      ▼
  Azure Service Bus
      msg.CorrelationId = "req-abc-123"
      ▼
  AnotherService consumer
      log: [AnotherService] [CID:req-abc-123] Processed ProjectCreated event

  ─────────────────────────────────────────────────────
  Search "req-abc-123" in Kibana/App Insights → full story
  Search TraceId in Jaeger                    → parent/child span tree
  Filter ServiceName="SampleMicroservice"    → only this service
