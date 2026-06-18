# Architecture

GrantAI KZ is a small Clean Architecture solution over Kazakhstan's master's
complex-testing (КТ) statistics — applications, participants and threshold
pass/fail counts per educational program group (ГОП). Dependencies point inwards
only: outer layers depend on inner ones, never the reverse. The domain has no
external dependencies at all, and the application layer depends only on the
domain plus a few cross-cutting abstractions.

## Layers

```mermaid
flowchart TD
    subgraph Hosts
        API["GrantAI.API<br/>(REST + Swagger)"]
        BOT["GrantAI.Bot<br/>(Telegram long-poll)"]
    end

    INFRA["GrantAI.Infrastructure<br/>MongoDB · Redis · ClosedXML · Serilog"]
    APP["GrantAI.Application<br/>use-cases · analytics · forecasting · probability · import"]
    DOMAIN["GrantAI.Domain<br/>entities · enums (no dependencies)"]

    API --> APP
    BOT --> APP
    API -. composition root .-> INFRA
    BOT -. composition root .-> INFRA
    INFRA --> APP
    APP --> DOMAIN
```

The two hosts reference Infrastructure **only** to register adapters in their
DI container at start-up (the composition root). All real work goes through
Application interfaces, so controllers and bot handlers never see MongoDB,
Redis or ClosedXML types.

## The dependency rule in practice

- **Domain** — `AdmissionRecord` (one ГОП per campaign: applications,
  participants, threshold pass/fail counts), `ImportLog`, and the `Season` /
  `TrendDirection` enums. Pure C# with no NuGet references. The Mongo `_id` is a
  deterministic natural key (`year|season|group`) built by
  `AdmissionRecord.BuildId(...)`, which makes re-imports idempotent.
- **Application** — the ports (`IAdmissionRepository`, `ICacheService`,
  `IWorkbookReader`) and the pure engines that implement the business logic.
  Forecasting, probability and analytics are all deterministic functions of the
  records passed in, which is what makes them easy to unit-test.
- **Infrastructure** — the adapters: `AdmissionRepository` (MongoDB.Driver),
  `RedisCacheService` (StackExchange.Redis), `ClosedXmlWorkbookReader`
  (ClosedXML) and the Serilog configurator. This is the only project that
  references those libraries.
- **API / Bot** — thin delivery mechanisms over the same `ISpecialtyQueryService`
  and `IExcelImportService`.

## Read path (cache-aside)

```mermaid
sequenceDiagram
    participant C as Client (HTTP / Telegram)
    participant Q as SpecialtyQueryService
    participant R as Redis (ICacheService)
    participant M as MongoDB (IAdmissionRepository)
    participant E as Engine (analytics / forecast / probability)

    C->>Q: GetForecastAsync("M094")
    Q->>R: GET grantai:forecast:M094
    alt cache hit
        R-->>Q: cached ForecastDto
    else cache miss
        Q->>M: GetByCodeAsync("M094")
        M-->>Q: records
        Q->>E: Forecast(records)
        E-->>Q: ForecastDto
        Q->>R: SET grantai:forecast:M094 (TTL)
    end
    Q-->>C: ForecastDto
```

## Write path (import + cache invalidation)

```mermaid
sequenceDiagram
    participant C as Client (POST /api/import)
    participant I as ExcelImportService
    participant W as IWorkbookReader (ClosedXML)
    participant M as MongoDB
    participant R as Redis

    C->>I: ImportAsync(stream, fileName)
    I->>W: Read(stream)
    W-->>I: raw sheets (every row as string cells)
    I->>I: SheetParser: find campaign (sheet name) + 2-row header
    I->>I: parse ГОП rows, validate, de-duplicate
    I->>M: BulkUpsert(records)  (idempotent by _id)
    I->>R: RemoveByPrefix("grantai:")  (drop stale reads)
    I-->>C: ImportResultDto (inserted / updated / duplicate / failed)
```

Because every cache key is namespaced under `grantai:`, a successful import
invalidates all derived reads in one `SCAN`-based sweep, so the next query
recomputes from fresh data.

## Why these choices

- **MongoDB without an ORM.** The data is denormalised statistics; a document
  per campaign row fits naturally and the driver's `ReplaceOneModel` upsert gives
  idempotent imports for free via the natural-key `_id`.
- **Pure engines.** Keeping forecasting/probability/analytics free of I/O means
  the maths is unit-tested directly, with no mocks.
- **Single source of truth for the pass rate.** The probability engine consumes
  the forecast engine, so the predicted pass rate behind "chance" always matches
  the "forecast" endpoint.
