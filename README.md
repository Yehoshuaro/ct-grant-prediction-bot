# GrantAI KZ

Telegram bot and REST API that analyse Kazakhstan master's-degree complex
testing (КТ) admission statistics and forecast, per program group (ГОП), how
hard the entrance threshold is to clear.

The bot runs as **@complex_test_kz_bot** ("Complex Test Bot for Kazakhstan").

## What it does

- Imports the official КТ result spreadsheets (one `.xlsx` per campaign) and
  stores per-ГОП records in MongoDB.
- For each program group computes participation rate and threshold pass rate
  from the raw counts: applications, participants, cleared the threshold,
  didn't clear it.
- Forecasts the next campaign's pass rate - an ordinary-least-squares trend
  blended with a recency-weighted moving average - and returns a likely range
  and a rough confidence figure.
- Serves it over a REST API and a Telegram bot: per-group summary, full
  history, campaign-over-campaign comparison, and an overall statistics view.

It answers *how competitive the threshold is for a program*, not *what score
you personally need* — see Limitations.

## Stack

.NET 10, ASP.NET Core (API), a generic-host worker (bot), Telegram.Bot,
MongoDB via MongoDB.Driver (no EF), Redis (StackExchange.Redis) for caching,
ClosedXML for spreadsheets, Serilog, FluentValidation, AutoMapper, Swagger.
Six projects in a Clean Architecture layout, xUnit tests, Docker Compose for
the whole stack.

## Running

Needs Docker. From the repo root:

    cp .env.example .env        # then set TELEGRAM_BOT_TOKEN
    docker compose up --build

This brings up MongoDB, Redis, the API (http://localhost:8080, Swagger at
`/swagger`) and the bot.

Get a token from @BotFather and put it in `.env` as `TELEGRAM_BOT_TOKEN`.
After editing `.env`, re-run `docker compose up -d` so the bot container is
recreated with the new value (a plain `restart` won't pick it up).

## Loading data

The database starts empty. Import campaign spreadsheets through the API:

    POST /api/import        # multipart/form-data, one or more .xlsx files

Swagger has a form for it. Re-importing the same file is idempotent — rows are
upserted, not duplicated. Sample files are in `sample-data/`.

## API

    GET  /api/specialities          all program groups in the data
    GET  /api/specialities/{code}   one group with its derived rates
    GET  /api/history/{code}        every campaign for a group
    GET  /api/forecast/{code}       next-campaign pass-rate forecast
    GET  /api/chance/{code}         probability of clearing the threshold (no score input)
    GET  /api/compare/{code}        campaign-over-campaign deltas
    GET  /api/statistics            totals across all imported data
    POST /api/import                import .xlsx campaign files

## Bot commands

`/start`, `/help`, `/speciality {code}` (alias `/specialty`),
`/history {code}`, `/forecast {code}`, `/chance {code}`, `/compare {code}`.
Codes are the ГОП codes — `M001`, `M094`, and so on.

## How the forecast works

Each campaign contributes one pass-rate point per group. The service fits an
OLS line over those points, separately takes a recency-weighted moving
average, then blends the two and clamps the result to [0, 100]. The spread of
recent points sets the likely range and the confidence. With only a handful of
campaigns this is a trend indicator, not a precise prediction, and the
responses say as much.

## Expected spreadsheet format

These are the official КТ statistics files, not a custom schema. The campaign
(year + season, e.g. `2024-зима-рус`) is encoded in the sheet name; the data
columns sit under a two-row merged header. Per ГОП row the importer reads:
code, name, applications, participants, cleared-threshold count, not-cleared
count. Percentage cells are recomputed from the counts rather than trusted.
Total rows (ИТОГО / Всего) and non-ГОП rows are skipped.

## Limitations

This analyses the **threshold** (порог) - the fixed, publicly known minimum
score every applicant must clear. It shows how many people clear it per group
and where that trend is heading. It does **not** predict grant cutoff scores
(the competitive проходной балл that actually wins a grant): the threshold
spreadsheets carry no individual scores and no grant data, so a cutoff can't be
derived from them.

Grant-cutoff prediction is the next phase. It works off the published
grant-awardee lists (which do contain per-applicant scores) and is being added
separately.

## Project layout

    src/
      GrantAI.Domain           entities, enums, domain logic
      GrantAI.Application       use cases, ports, DTOs
      GrantAI.Infrastructure    MongoDB, Redis, ClosedXML, Serilog adapters
      GrantAI.API               ASP.NET Core controllers
      GrantAI.Bot               Telegram long-polling worker
    tests/
      GrantAI.Tests            xUnit tests
