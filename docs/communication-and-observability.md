# Communication & Observability Guide

## 1. How Monolith and Microservice Talk to Each Other

Think of the monolith as the **main office** and the microservice as a **new branch office**.

They can communicate in two ways:

---

### Way 1 — Direct Phone Call (Synchronous HTTP)

The monolith picks up the phone, calls the microservice, waits for the answer, then continues its work.

```
User Request
    │
    ▼
Monolith  ──── calls ────▶  SampleMicroservice
              (waits)           (responds)
    │◄───────────────────────────┘
    ▼
Returns result to user
```

**When to use:** When the monolith needs the microservice's answer before it can move forward.

**Example:** *"Give me the list of sample items so I can include them in this report."*

---

### Way 2 — Dropping a Note in a Mailbox (Async via Service Bus)

The monolith drops a message in a shared mailbox (Azure Service Bus) and moves on. The microservice picks it up later and handles it independently.

```
Monolith  ──── drops message ────▶  [Service Bus]
(moves on immediately)                    │
                                          ▼
                              SampleMicroservice picks it up
                              and handles it on its own time
```

**When to use:** When the monolith doesn't need to wait for the result.

**Example:** *"A project was created. Whoever cares about that, deal with it."*

---

## 2. How Microservices Talk to Each Other

Exactly the same two ways. No microservice talks directly to another's database. They only communicate through calls or messages.

### Direct Call — when you need an answer now

```
ServiceA  ──── HTTP call ────▶  ServiceB  ──── responds ────▶  ServiceA
```

### Message / Event — when you're just announcing something happened

```
ServiceA drops a message: "Order was placed"
         │
    [Shared Mailbox]
         ├──▶  BillingService  handles it (creates invoice)
         └──▶  NotifyService   handles it (sends email)
```

ServiceA has no idea who BillingService or NotifyService are. It just announces what happened. Anyone who cares can listen. This keeps services completely independent.

---

## 3. Logs and Traces — How to Know What's Happening and Where

### The Problem Without a Solution

Imagine two services writing logs separately:

```
Toa.Api log:            "Project created"
SampleMicroservice log: "Something went wrong"
```

You can't tell if these two are related. Was the error caused by that project creation? No way to know.

---

### The Solution — A Tracking ID (Correlation ID)

Every request gets a unique ID — like a **parcel tracking number**. Every service stamps that ID on every log line it writes for that request.

```
Request comes in with ID: req-abc-123
       │
       ▼
Toa.Api logs:            [Toa.Api]            [req-abc-123]  Project created
       │ passes ID forward
       ▼
SampleMicroservice logs: [SampleMicroservice] [req-abc-123]  Item created
       │ passes ID forward (via Service Bus message)
       ▼
AnotherService logs:     [AnotherService]     [req-abc-123]  Event processed
```

Now search `req-abc-123` in your log tool — you instantly see the **full journey** across all three services.

---

### How to Tell Which Service a Log Belongs To

Every log line is stamped with the **service name**:

```
[09:15:04] [Toa.Api]            [req-abc-123]  Project created
[09:15:04] [SampleMicroservice] [req-abc-123]  Sample item created
[09:15:05] [SampleMicroservice] [req-abc-123]  ERROR: Something failed
```

### Search Reference

| What you want | How to search |
|---|---|
| Everything from SampleMicroservice only | `ServiceName = "SampleMicroservice"` |
| Full journey of one request across all services | `CorrelationId = "req-abc-123"` |
| All errors everywhere | `Level = ERROR` |
| One user's activity | `UserId = "user-guid"` |

---

## 4. What's Already Done vs What Still Needs to Be Done

### SampleMicroservice

| Feature | Status |
|---|---|
| Stamps `ServiceName` on every log line | ✅ Done |
| Reads / generates `Correlation ID` per request | ✅ Done |
| Stamps `Correlation ID` on every log line | ✅ Done |
| Forwards `Correlation ID` when calling other services | ✅ Done |
| OpenTelemetry tracing (visual span tree) | ✅ Done — needs an endpoint configured |

### Monolith (Toa.Api) — Still Needed

| Feature | Status |
|---|---|
| Add `ServiceName = "Toa.Api"` to its Serilog config | ⬜ Not done |
| Forward `Correlation ID` header when calling the microservice | ⬜ Not done |
| Point both services at the same log sink (Elasticsearch / App Insights) | ⬜ Not done |

---

## 5. Complete End-to-End Flow

```
Browser / APIM
    │  X-Correlation-Id: req-abc-123
    ▼
Toa.Api (Monolith)
    [Toa.Api] [req-abc-123]  Handling CreateProject
    │  HttpClient forwards X-Correlation-Id header
    ▼
SampleMicroservice
    [SampleMicroservice] [req-abc-123]  Sample item created
    │  Service Bus message carries CorrelationId
    ▼
AnotherService
    [AnotherService] [req-abc-123]  Processed ProjectCreated event

─────────────────────────────────────────────────────────────────
Search "req-abc-123" in Kibana / App Insights  →  full story
Filter ServiceName = "SampleMicroservice"      →  only this service
Filter Level = ERROR                           →  all errors everywhere
```

Once both services write logs to the **same place** with the **same tracking ID**, you have one single view of everything happening across your entire system — regardless of how many services you add in the future.
