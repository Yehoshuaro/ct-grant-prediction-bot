# GrantAI KZ

Telegram bot and REST API that analyse Kazakhstan master's-degree complex
testing (КТ) admission statistics and forecast, per program group (ГОП), how
hard the entrance threshold is to clear, plus a forecast of the next-intake
grant cutoff (проходной балл на грант) per master's track.

The bot runs as **@complex_test_kz_bot** ("Complex Test Bot for Kazakhstan").

## What it does

- Imports the official КТ result spreadsheets (one `.xlsx` per campaign) and
  the published grant-list PDFs, storing per-ГОП records in MongoDB.
- For each program group computes participation rate and threshold pass rate
  from the raw counts: applications, participants, cleared the threshold,
  didn't clear it.
- Forecasts the next campaign's pass rate (ordinary-least-squares trend
  blended with a recency-weighted moving average) and the next intake's grant
  cutoff per track, with a likely range and a rough confidence figure.
- Serves it over a REST API and a Telegram bot: per-group summary, full
  history, campaign-over-campaign comparison, and an overall statistics view.

`/chance` answers *how competitive the threshold is for a program*, not *what
score you personally need*, and not the chance of winning a grant. For grant
estimates use `/grant`. See **Limitations**.

## Stack

.NET 10, ASP.NET Core (API), a generic-host worker (bot), Telegram.Bot,
MongoDB via MongoDB.Driver (no EF), Redis (StackExchange.Redis) for caching,
ClosedXML for spreadsheets, PdfPig for grant PDFs, Serilog for logs,
OpenTelemetry for traces and metrics, FluentValidation, AutoMapper, Swagger.
Six projects in a Clean Architecture layout, xUnit tests, Docker Compose for
the whole stack.

## Running

Needs Docker. From the repo root:

    cp .env.example .env        # then set TELEGRAM_BOT_TOKEN
    docker compose up --build

This brings up MongoDB, Redis, the API (http://localhost:8080, Swagger at
`/swagger`, liveness at `/health`, readiness at `/health/ready`) and the bot.

Get a token from @BotFather and put it in `.env` as `TELEGRAM_BOT_TOKEN`.
After editing `.env`, re-run `docker compose up -d` so the bot container is
recreated with the new value (a plain `restart` won't pick it up).

## Loading data

The database starts empty. Import campaign spreadsheets through the API:

    POST /api/import         # multipart/form-data, one or more .xlsx files
    POST /api/grants/import  # multipart/form-data, one or more .pdf files

Swagger has a form for both. Re-importing the same file is idempotent: rows
are upserted, not duplicated. Sample files are in `sample-data/`.

## API

    GET  /api/specialities          all program groups in the data
    GET  /api/specialities/{code}   one group with its derived rates
    GET  /api/history/{code}        every campaign for a group
    GET  /api/forecast/{code}       next-campaign pass-rate forecast
    GET  /api/chance/{code}         probability of clearing the threshold
    GET  /api/compare/{code}        campaign-over-campaign deltas
    GET  /api/statistics            totals across all imported data
    POST /api/import                import .xlsx campaign files
    GET  /api/grants                grant summaries (latest year per track)
    GET  /api/grants/{code}         grant-cutoff history per year, per track
    GET  /api/grants/{code}/forecast  next-intake grant-cutoff forecast
    POST /api/grants/import         import grant-list .pdf files
    GET  /health                    liveness (no dependencies)
    GET  /health/ready              readiness (Mongo + Redis ping)

Errors are returned as RFC 7807 ProblemDetails. Rate limiter rejections come
back as `429` with a `Retry-After` header.

## Bot commands

The Telegram bot replies in Russian. Commands stay on the latin keyboard
because Telegram requires that. Inline buttons sit under the welcome / help
screens and under every per-code summary, so users can jump between
`/forecast`, `/history`, `/chance`, `/compare` and `/grant` without typing.

| Command              | Russian description                       |
|----------------------|-------------------------------------------|
| `/start`, `/help`    | Запуск и краткое описание; список команд. |
| `/speciality <code>` | Сводка по ГОП за последнюю кампанию.      |
| `/history <code>`    | История кампаний и тренды.                |
| `/forecast <code>`   | Прогноз доли участников, прошедших порог. |
| `/chance <code>`     | Шанс пройти порог в этой группе.          |
| `/compare <code>`    | Сравнение лета и зимы.                    |
| `/grant <code>`      | Прогноз проходного балла на грант.        |

Codes are the ГОП codes: `M001`, `M094`, and so on. `/specialty` (US spelling)
is accepted as an alias for `/speciality`.

## Rate limiting

The API uses ASP.NET Core's built-in rate limiter, partitioned by client IP
(`X-Forwarded-For` is trusted when present, on the assumption of a single
trusted reverse proxy). Two policies:

- `global` — fixed window, applied to every request except `/health*`.
- `strict` — tighter window for the expensive endpoints: `POST /api/import`,
  `POST /api/grants/import`, `GET /api/forecast/{code}`, `GET /api/chance/{code}`
  and `GET /api/grants/{code}/forecast`.

The bot has its own per-chat sliding-window throttle (in-memory; the bot runs
as a single long-polling process so memory is enough). Limits are configurable
under `RateLimit:*` (API) and `BotRateLimit` (bot) in `appsettings.json`.

## Observability

OpenTelemetry tracing + metrics is configured in both hosts:

- ASP.NET Core, HttpClient and runtime instrumentation.
- Custom `GrantAI.Application` source for forecast/import spans + counters.
- OTLP exporter only fires when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, so local
  runs without a collector still start cleanly.

Serilog is still in charge of structured logs (one source of truth for log
output, OTel for traces and metrics).

## How the forecast works

Each campaign contributes one pass-rate point per group. The forecast
service:

1. Collapses raw rows into one (campaign-ordinal, pass-rate) point per
   campaign.
2. Fits an OLS line over those points and predicts the next ordinal.
3. Computes a recency-weighted moving average of the recent pass rates.
4. Blends the two predictions; the regression's weight is its R².
5. Clamps the result to [0, 100] and to a plausible range around the
   observed minimum/maximum.
6. Reports a confidence (fit × data volume × residual stability, clamped to
   [0.30, 0.95]) and a prediction interval whose width grows on small samples
   and away from the centroid.

The slope-per-year used for trend classification is derived from the actual
cadence in the data (campaigns per year inferred from the ordinal span), so a
group with summer-only intakes is not misread as having a 2× slope.

`/chance` reuses the same forecast and reports the same pass rate as a
percentage; it is the share of *participants* that historically clear the
threshold, not the share of *individuals* with any given score, and not the
chance of winning a grant.

`/grant` is a separate model over the published grant-cutoff PDFs. It is
deliberately a directional estimate (confidence is capped at 0.70) because
only 2-3 yearly points typically exist per (group, track).

## Architectural notes

- **Result<T> at the boundary.** Read services return `Result<T>` (success or
  a tagged `Error`) instead of `null` / "empty-DTO" sentinels; the API
  translates that to 200/404/400 through a tiny helper and the bot to a
  Russian "not found" reply.
- **MediatR / CQRS is intentionally deferred.** Introducing it half-way
  (e.g. reads only) would leave the use-case layer inconsistent. Worth a look
  if the project grows past two hosts; not worth the churn today.

## Expected spreadsheet format

These are the official КТ statistics files, not a custom schema. The campaign
(year + season, e.g. `2024-зима-рус`) is encoded in the sheet name; the data
columns sit under a two-row merged header. Per ГОП row the importer reads:
code, name, applications, participants, cleared-threshold count, not-cleared
count. Percentage cells are recomputed from the counts rather than trusted.
Total rows (ИТОГО / Всего) and non-ГОП rows are skipped.

## Limitations

`/chance` analyses the **threshold** (порог): the fixed, publicly known
minimum score every applicant must clear. It shows how many people clear it
per group and where that trend is heading. It does **not** predict grant
cutoff scores: for that, use `/grant`.

`/grant` operates on the published grant-awardee lists (which carry
per-applicant scores). Because typically only 2-3 yearly intakes are on
record, treat the figure as a guideline rather than a precise number.

## Project layout

    src/
      GrantAI.Domain           entities, enums, domain logic
      GrantAI.Application      use cases, ports, DTOs, Result<T>, OTel sources
      GrantAI.Infrastructure   MongoDB, Redis, ClosedXML, PdfPig, Serilog adapters
      GrantAI.API              ASP.NET Core controllers, rate limiter, health checks
      GrantAI.Bot              Telegram long-polling worker, inline keyboards
    tests/
      GrantAI.Tests            xUnit tests
    .github/workflows/         CI (build/test) + Docker (GHCR push)
