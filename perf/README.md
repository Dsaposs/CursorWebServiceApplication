# Performance & load testing

Runs API load probes (k6), long-session soak/memory sampling, and Playwright UI latency measurements. Outputs a single report:

**`reports/PERFORMANCE_REPORT.md`**

## Quick start (Docker stack)

```powershell
# From repo root — starts stack and runs full pipeline
.\scripts\run-perf.cmd
```

Or manually:

```powershell
docker compose up -d
cd perf
npm install
npm run pipeline
```

## Pipeline stages

| Stage | Command | Purpose |
|-------|---------|---------|
| Bootstrap | `npm run bootstrap` | Creates game/session + player pool for load tests |
| k6 load | `npm run test:k6` | Stepped VU probes: mixed poll, player poll, action throughput |
| Soak | `npm run soak` | Long-running submit/resolve loop + Docker memory samples |
| UI latency | `npm run test:ui` | Page load + interaction timings (Playwright) |
| Report | `npm run report` | Compiles `PERFORMANCE_REPORT.md` |

## Requirements

- Running API at `http://localhost:5294` (Docker recommended: Postgres + Redis)
- **k6** via [Docker `grafana/k6`](https://hub.docker.com/r/grafana/k6) (auto-detected) or local `k6` install
- Playwright Chromium (installed by pipeline)

## Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PERF_API_URL` | `http://localhost:5294` | API base URL |
| `PERF_UI_URL` | `http://localhost:3000` | UI base URL |
| `PERF_PLAYER_POOL` | `100` | Players pre-joined in bootstrap |
| `PERF_K6_VUS_STEPS` | `5,10,25,50,75,100,150,200` | VU levels for stepped probes |
| `PERF_K6_STEP_DURATION_SEC` | `45` | Duration per step |
| `PERF_K6_DEGRADE_ERROR_RATE` | `0.02` | Error rate marking degradation |
| `PERF_K6_DEGRADE_P95_MS` | `1500` | p95 latency marking degradation |
| `PERF_K6_FAIL_ERROR_RATE` | `0.10` | Error rate stopping further steps |
| `PERF_SOAK_DURATION_MIN` | `5` | Soak test length (use 30–60 for long campaigns) |
| `PERF_UI_RUNS` | `5` | Repetitions per UI latency page |
| `PERF_SKIP_K6` / `PERF_SKIP_SOAK` / `PERF_SKIP_UI` | — | Skip individual stages |

## Output artifacts

```
perf/reports/
  PERFORMANCE_REPORT.md    ← compiled summary
  raw/
    fixture.json
    k6-*.json
    k6-stepped-summary.json
    soak-results.json
    ui-latency.json
```
