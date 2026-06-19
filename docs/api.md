# API & bot reference

The REST API is self-documented with Swagger at `/swagger` when the API is
running. This page is a quick reference with example payloads. All responses are
JSON with camelCase property names; enums are serialised as strings.

## REST endpoints

| Method | Route                          | Description                                       | Not-found behaviour |
|--------|--------------------------------|---------------------------------------------------|---------------------|
| GET    | `/api/specialities`            | All groups with a latest-campaign summary.        | — |
| GET    | `/api/specialities/{code}`     | Summary for one group.                            | 404 if unknown. |
| GET    | `/api/history/{code}`          | Full campaign history + trend directions.         | 404 if no campaigns. |
| GET    | `/api/forecast/{code}`         | Forecast of the next campaign's pass rate.        | 404 if no data. |
| GET    | `/api/chance/{code}`           | Probability of clearing the threshold.            | 404 if no data. |
| GET    | `/api/compare/{code}`          | Summer-vs-winter comparison.                      | 404 if no data. |
| GET    | `/api/statistics`              | Database-wide overview.                           | — |
| POST   | `/api/import`                  | Upload one or more `.xlsx` files (multipart).     | 400 if no files. |
| GET    | `/api/grants`                  | All ГОПы with a latest-year grant-cutoff summary. | — |
| GET    | `/api/grants/{code}`           | Grant-cutoff history per year, per track.         | 404 if no data. |
| GET    | `/api/grants/{code}/forecast`  | Forecast of the next intake's grant cutoff.       | 404 if no data. |
| POST   | `/api/grants/import`           | Upload one or more grant-list `.pdf` files.       | 400 if no files. |

### GET `/api/specialities/{code}`

`GET /api/specialities/M094`

```json
{
  "code": "M094",
  "name": "Математика и статистика",
  "campaignCount": 5,
  "latestYear": 2025,
  "latestSeason": "Summer",
  "latestApplications": 2381,
  "latestParticipants": 2290,
  "latestPassRate": 56.2
}
```

### GET `/api/forecast/{code}`

`GET /api/forecast/M094`

```json
{
  "code": "M094",
  "predictedPassRate": 57.8,
  "lowerBound": 52.1,
  "upperBound": 63.5,
  "confidencePercent": 68,
  "trend": "Rising",
  "dataPoints": 5,
  "method": "Linear regression blended with a recency-weighted moving average",
  "factors": [
    "Pass rates show a clear upward trend (~3.4 points/year).",
    "Applications to this group have been rising over recent campaigns.",
    "Forecast is based on 5 campaigns of history."
  ],
  "explanation": "Predicted threshold pass rate for the next 'M094' campaign is 57.8% (range 52.1–63.5%, confidence 68%). The pass-rate trend is rising."
}
```

### GET `/api/chance/{code}`

`GET /api/chance/M094`

```json
{
  "code": "M094",
  "passProbabilityPercent": 58,
  "lowerBoundPercent": 52,
  "upperBoundPercent": 64,
  "predictedPassRate": 57.8,
  "confidencePercent": 68,
  "dataPoints": 5,
  "factors": [
    "In the latest campaign (2025 Summer), 1287 of 2290 participants cleared the threshold (56.2%).",
    "Pass-rate trend for this group is rising.",
    "Estimate uses 5 campaigns of history."
  ],
  "explanation": "For 'M094', the estimated probability of clearing the entrance threshold is about 58% (range 52–64%), based on the forecasted pass rate. This reflects the group's historical pass rate, not an individual candidate's ability."
}
```

There is intentionally **no score parameter**: the published data has no
per-applicant scores, so "chance" is the group's forecasted threshold pass rate.

### GET `/api/statistics`

```json
{
  "totalRecords": 683,
  "totalGroups": 160,
  "earliestYear": 2023,
  "latestYear": 2025,
  "totalApplications": 195456,
  "totalParticipants": 145687,
  "totalPassed": 65000,
  "overallParticipationRate": 74.5,
  "overallPassRate": 44.6
}
```

### POST `/api/import`

Multipart form upload; the file field is named `files` and may be repeated.

```bash
curl -X POST http://localhost:8080/api/import \
  -F "files=@sample-data/kt-2024-summer.xlsx" \
  -F "files=@sample-data/kt-2025-summer.xlsx"
```

```json
[
  {
    "fileName": "kt-2024-summer.xlsx",
    "totalRows": 149,
    "inserted": 149,
    "updated": 0,
    "duplicates": 0,
    "failed": 0,
    "durationMs": 120,
    "errors": []
  }
]
```

`totalRows` counts genuine ГОП rows only — the title, blank rows, the
`ИТОГО`/`Всего` total and any non-ГОП line are skipped before counting.
Re-importing the same file reports its rows as **updated** rather than inserted.

### GET `/api/grants/{code}/forecast`

`GET /api/grants/M094/forecast`

```json
[
  {
    "code": "M094",
    "name": "Информационные технологии",
    "masterType": "ScientificPedagogical",
    "scoreScaleMax": 150,
    "predictedCutoff": 132,
    "lowerBound": 124,
    "upperBound": 140,
    "confidencePercent": 55,
    "trend": "Rising",
    "dataPoints": 3,
    "method": "Linear regression blended with a recency-weighted moving average",
    "factors": [
      "Grant cutoffs show a weak upward trend (~3.5 points/year on a 0–150 scale).",
      "Forecast is based on 3 intake year(s) of grant data on the 150-point scale.",
      "Grant cutoffs swing year-to-year (quota effects, applicant pools); treat the figure as a guideline."
    ],
    "explanation": "To win a grant in 'M094' (научно-педагогическая) next intake, you would likely need around 132 out of 150 points (range 124–140, confidence 55%). Trend: rising."
  }
]
```

The endpoint always returns one entry per master's track present in the data
(typically one or two). Profile and Scientific-Pedagogical scores are reported
on their own scales (0–70 and 0–150 respectively) and never mixed together.

### POST `/api/grants/import`

Multipart form upload; the file field is named `files` and may be repeated.

```bash
curl -X POST http://localhost:8080/api/grants/import \
  -F "files=@data/grants/grants-2024.pdf" \
  -F "files=@data/grants/grants-2025.pdf"
```

```json
[
  {
    "fileName": "grants-2025.pdf",
    "year": 2025,
    "blocks": 128,
    "inserted": 128,
    "updated": 0,
    "durationMs": 4200,
    "error": null
  }
]
```

`blocks` is the number of distinct ГОП blocks parsed across both master's
tracks. Re-importing the same PDF reports its records as **updated** rather
than inserted (the natural-key `_id` makes the operation idempotent).

## Telegram bot commands

| Command               | Example            | Result |
|-----------------------|--------------------|--------|
| `/start`, `/help`     | `/help`            | Welcome / command list. |
| `/speciality <code>`  | `/speciality M094` | Latest-campaign summary for a group. |
| `/history <code>`     | `/history M094`    | Campaign-by-campaign history with trends. |
| `/forecast <code>`    | `/forecast M094`   | Next-campaign pass-rate forecast. |
| `/chance <code>`      | `/chance M094`     | Probability of clearing the threshold. |
| `/compare <code>`     | `/compare M094`    | Summer vs winter. |
| `/grant <code>`       | `/grant M094`      | Next-intake grant-cutoff forecast (per master's track). |

`/specialty` (US spelling) is accepted as an alias for `/speciality`. Commands
sent with the bot's @-mention (e.g. `/forecast@MyBot M094`) are handled too.

The numbers above are illustrative; exact values depend on the data you import.
