# Communication & Observability — Technical Reference

Since the SampleMicroservice code is already updated, this document covers all three topics with direct references to what's built and what's needed for the monolith side.

---

## 1. Monolith ↔ Microservice Communication

Two patterns depending on whether you need a response or not.

### Synchronous (need a response back)

```
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
```

The `CorrelationIdDelegatingHandler` is **already built** in the microservice at:

```
src/Infrastructure/Http/CorrelationIdDelegatingHandler.cs
```

In the **monolith**, register it the same way whenever calling the microservice:

```csharp
// Toa.Api — Infrastructure/Startup.cs
services.AddTransient<CorrelationIdDelegatingHandler>();

services.AddHttpClient<ISampleItemService, SampleItemService>(client =>
{
    client.BaseAddress = new Uri(config["Services:SampleMicroservice:BaseUrl"]!);
})
.AddHttpMessageHandler<CorrelationIdDelegatingHandler>(); // forwards X-Correlation-Id
```

```csharp
// Toa.Api — Infrastructure/SampleService/SampleItemService.cs
public class SampleItemService : ISampleItemService, ITransientService
{
    private readonly HttpClient _client;
    public SampleItemService(HttpClient client) => _client = client;

    public Task<ApiResponse<List<SampleItemDto>>> GetSampleItemsAsync(CancellationToken ct = default)
        => _client.GetFromJsonAsync<ApiResponse<List<SampleItemDto>>>("api/v1/sample-items", ct)!;
}
```

Called from a MediatR handler exactly like any other service already in the monolith:

```csharp
// in any monolith handler
public async Task<ApiResponse<...>> Handle(SomeRequest request, CancellationToken ct)
{
    var items = await _sampleItemService.GetSampleItemsAsync(ct);
    ...
}
```

---

### Asynchronous (fire and forget / event-driven)

Use this when the monolith does not need to wait for the microservice to finish.

```
Monolith publishes event to Azure Service Bus Topic
         │
         ▼
   [project-events topic]
         │
         ├──► subscription: sample-svc  ──► SampleMicroservice processes it
         └──► subscription: other-svc   ──► AnotherService processes it
```

```csharp
// Monolith — publishes after a project is created
await _serviceBusClient.SendMessageAsync(new ServiceBusMessage(
    JsonSerializer.Serialize(new ProjectCreatedEvent(project.Id, project.Name)))
{
    Subject       = "ProjectCreated",
    CorrelationId = correlationId   // carry the same CorrelationId into the bus
});
```

```csharp
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
```

> **Key rule:** Always carry `CorrelationId` through the Service Bus message `CorrelationId` property and restore it on the consumer side so logs stay linked across async boundaries.

---

## 2. Microservice ↔ Microservice Communication

Same two patterns. The only difference is there is no monolith involved.

```
┌─────────────────┐         ┌──────────────────┐        ┌──────────────────┐
│ SampleMicroSvc  │──HTTP──▶│  BillingMicroSvc  │        │  NotifMicroSvc   │
│                 │         └──────────────────┘        └──────────────────┘
│                 │──────── Service Bus Topic ──────────────────────────────▶│
└─────────────────┘         (async, no coupling)
```

### Sync — typed client with CorrelationId forwarding

Every microservice registers `CorrelationIdDelegatingHandler` (already in SampleMicroservice) and attaches it to each named `HttpClient`:

```csharp
// ServiceA calling ServiceB
services.AddHttpClient<IBillingService, BillingService>(client =>
    client.BaseAddress = new Uri(config["Services:BillingService:BaseUrl"]!))
.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
```

The `CorrelationIdDelegatingHandler` reads the `X-Correlation-Id` from the **current** request and injects it into the **outbound** request — automatically, with no handler code change required.

### Async — one publisher, many independent consumers

```
ServiceA publishes "SampleItemCreated"
         │
    [sample-item-events topic]
         ├──► BillingService  (creates invoice)
         └──► AuditService    (records audit trail)

ServiceA has NO knowledge of BillingService or AuditService.
Adding a new consumer requires zero change to ServiceA.
```

### Decision Guide

| Scenario | Use |
|---|---|
| Need the response to continue | Sync HTTP |
| Triggering work that runs independently | Async Service Bus |
| Multiple services react to the same thing | Service Bus Topic |
| High volume stream (telemetry, clicks) | Azure Event Hubs |

---

## 3. Traces and Logs — What's Built vs What to Configure

### What's already running in SampleMicroservice

#### `CorrelationIdMiddleware` — `src/Infrastructure/Middleware/CorrelationIdMiddleware.cs`

Every inbound request:
1. Reads `X-Correlation-Id` header (or generates a new one if absent)
2. Pushes it into Serilog `LogContext` for every log in that request
3. Echoes it back on the response header

#### `ServiceName` + `CorrelationId` in every log line — `src/Host/Program.cs`

```csharp
.Enrich.WithProperty("ServiceName", serviceName)
.WriteTo.Console(outputTemplate:
    "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [CID:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
```

#### OpenTelemetry — `src/Infrastructure/Startup.cs`

```csharp
services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(...)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(...)   // activated only when endpoint is set
    );
```

---

### What your logs look like right now (no extra setup needed)

```
[09:15:01 INF] [SampleMicroservice] [CID:f3a1-...]       Server booting up...
[09:15:04 INF] [SampleMicroservice] [CID:req-abc-123]    Sample items retrieved successfully.
[09:15:05 ERR] [SampleMicroservice] [CID:req-abc-123]    Request failed with Status Code 404 and Error Id err-xyz.
```

Monolith logs (once you add `ServiceName` enrichment there too):

```
[09:15:04 INF] [Toa.Api]            [CID:req-abc-123]    Project created.
[09:15:04 INF] [Toa.Api]            [CID:req-abc-123]    Calling SampleMicroservice...
[09:15:04 INF] [SampleMicroservice] [CID:req-abc-123]    Sample item created successfully.
```

Same `CID` across both services. One search finds the full journey.

---

### How to identify a specific service's traces

| What you want to find | Search by |
|---|---|
| All logs from this microservice only | `ServiceName = "SampleMicroservice"` |
| Everything for one user request (cross-service) | `CorrelationId = "req-abc-123"` |
| Full distributed trace (parent/child spans) | `TraceId = "4bf92f35..."` (from OTEL) |
| All errors across every service | `Level = ERROR` |
| One request's path across async Service Bus | `CorrelationId` (passed through `msg.CorrelationId`) |

---

### To enable a centralized log backend (point both services at the same sink)

```json
// logger.json in SampleMicroservice (already exists)
{
  "LoggerSettings": {
    "AppName": "SampleMicroservice",
    "ElasticSearchUrl": "http://elk:9200"
  }
}
```

```json
// logger.json in Toa.Api monolith
{
  "LoggerSettings": {
    "AppName": "Toa.Api",
    "ElasticSearchUrl": "http://elk:9200"
  }
}
```

Both services write to the same Elasticsearch index. Kibana searches by `ServiceName` or `CorrelationId` across both. No code change needed — only config.

---

### To enable distributed tracing (Jaeger / Grafana Tempo / Azure Monitor)

```json
// appsettings.json in SampleMicroservice (already has this key)
{
  "OpenTelemetry": {
    "Endpoint": "http://your-otel-collector:4317"
  }
}
```

Set that one value and the OTLP exporter activates. The trace spans from both the monolith and SampleMicroservice will appear as a single tree in the tracing UI — linked by the W3C `traceparent` header that `HttpClient` forwards automatically.

---

## 4. Complete Flow End-to-End

```
Browser/APIM
    │  X-Correlation-Id: req-abc-123
    │  traceparent: 00-traceId-spanId-01
    ▼
Toa.Api Monolith
    log: [Toa.Api] [CID:req-abc-123]  Handling CreateProject
    │  HttpClient forwards both headers downstream
    ▼
SampleMicroservice
    log: [SampleMicroservice] [CID:req-abc-123]  Sample item created
    ▼
Azure Service Bus
    msg.CorrelationId = "req-abc-123"
    ▼
AnotherService consumer
    log: [AnotherService] [CID:req-abc-123]  Processed ProjectCreated event

─────────────────────────────────────────────────────────────────
Search "req-abc-123" in Kibana / App Insights  →  full story
Search TraceId in Jaeger                       →  parent/child span tree
Filter ServiceName = "SampleMicroservice"      →  only this service
```
