# TTRPG Table — Performance Report

- **Generated**: 2026-05-22T17:47:13.798Z
- **API target**: http://localhost:5294
- **UI target**: http://localhost:3000
- **Fixture session**: f26bbcba-32a8-463f-9cae-34e08b29de83 (15 pre-joined players)
- **Degradation thresholds**: error ≥ 2%, p95 ≥ 1500 ms

## Executive summary

| Metric | Value |
| --- | --- |
| Baseline health p95 | 5 ms |
| Mixed polling — max stable VUs | **25** |
| Mixed polling — peak throughput | **221.79/s** |
| Player polling — max stable VUs | **25** |
| Action submit — max stable VUs | **0** |
| Action submit — peak throughput | **0/s** |
| Degradation threshold | error ≥ 2% or p95 ≥ 1500 ms |
| Failure threshold | error ≥ 10% |
| Soak actions resolved | 5 |
| Peak API memory (soak) | 480.5 MiB |
| UI player session load p95 | 23.8 ms |
| UI DM session load p95 | 14 ms |

## UI latency (primary)

Measured with Playwright over **2** runs per page (production UI @ http://localhost:3000).

### Page load

| Page | TTFB | DOMContentLoaded | Load complete |
| --- | --- | --- | --- |
| login | p50 4.7 ms, p95 79.7 ms, p99 79.7 ms | p50 11.7 ms, p95 148.8 ms, p99 148.8 ms | p50 12.1 ms, p95 148.8 ms, p99 148.8 ms |
| games_hub | p50 4 ms, p95 11.3 ms, p99 11.3 ms | p50 12.5 ms, p95 26.7 ms, p99 26.7 ms | p50 13.1 ms, p95 35.3 ms, p99 35.3 ms |
| dm_session | p50 2.5 ms, p95 2.5 ms, p99 2.5 ms | p50 11.1 ms, p95 11.3 ms, p99 11.3 ms | p50 11.5 ms, p95 14 ms, p99 14 ms |
| player_session | p50 2.2 ms, p95 4.5 ms, p99 4.5 ms | p50 17.6 ms, p95 23.1 ms, p99 23.1 ms | p50 17.9 ms, p95 23.8 ms, p99 23.8 ms |

### Interaction latency

| Interaction | p50 | p95 | p99 |
| --- | --- | --- | --- |
| dm_expand_pending_actions | 59 ms | 85 ms | 85 ms |
| player_open_action_form | 50 ms | 63 ms | 63 ms |

**Notes**
- TTFB includes Nuxt SSR/BFF proxy time to the API.
- Player session metrics assume bootstrap fixture and seeded player token.

## API load & throughput

Stepped k6 probes increase virtual users until error rate or latency thresholds are crossed.

### Mixed poll (player + DM)

| VUs | Throughput | Error rate | p95 | p99 | Run failed |
| --- | --- | --- | --- | --- | --- |
| 5 | 46.21/s | 0% | 13.1 ms | 0 ms | no |
| 10 | 91.15/s | 0% | 17 ms | 0 ms | no |
| 25 | 221.79/s | 0% | 27.6 ms | 0 ms | no |

- **Max stable VUs** (below degradation thresholds): **25**
- **Peak throughput observed**: **221.79/s**
- **Degradation begins**: Not reached in tested range
- **Failure threshold crossed**: Not reached in tested range

### Player polling

| VUs | Throughput | Error rate | p95 | p99 | Run failed |
| --- | --- | --- | --- | --- | --- |
| 5 | 23.85/s | 0% | 13.5 ms | 0 ms | no |
| 10 | 47.28/s | 0% | 17 ms | 0 ms | no |
| 25 | 117.69/s | 0% | 23 ms | 0 ms | no |

- **Max stable VUs** (below degradation thresholds): **25**
- **Peak throughput observed**: **117.69/s**
- **Degradation begins**: Not reached in tested range
- **Failure threshold crossed**: Not reached in tested range

### Action submit throughput

| VUs | Throughput | Error rate | p95 | p99 | Run failed |
| --- | --- | --- | --- | --- | --- |
| 5 | 0/s | 100% | 0 ms | 0 ms | yes |

- **Max stable VUs** (below degradation thresholds): **unknown**
- **Peak throughput observed**: **0/s**
- **Degradation begins**: 5 VUs (100% errors, p95 0 ms)
- **Failure threshold crossed**: 5 VUs (100% errors)

### Concurrent user ramp

- Ramp scenario peak: 137.37/s @ p95 403.3 ms, error rate 0%

## Long-running session & memory

- **Soak duration**: 1 min
- **Actions submitted & resolved**: 5
- **Final session version**: 31
- **Average live fetch latency**: 18.3 ms
- **Largest incremental live payload**: 27 KiB
- **Peak API container memory (sampled)**: 480.5 MiB
- **API memory delta (first → last sample)**: -943718 B

| Elapsed (s) | Container | Memory |
| --- | --- | --- |
| 0 | cursorwebserviceapplication-mobile-1 | 25.9 MiB |
| 0 | cursorwebserviceapplication-api-1 | 480.5 MiB |
| 0 | cursorwebserviceapplication-ui-1 | 48.7 MiB |
| 0 | cursorwebserviceapplication-redis-1 | 9.5 MiB |
| 0 | cursorwebserviceapplication-postgres-1 | 318.2 MiB |
| 44 | cursorwebserviceapplication-mobile-1 | 25.9 MiB |
| 44 | cursorwebserviceapplication-api-1 | 479.6 MiB |
| 44 | cursorwebserviceapplication-ui-1 | 48.7 MiB |
| 44 | cursorwebserviceapplication-redis-1 | 9.5 MiB |
| 44 | cursorwebserviceapplication-postgres-1 | 319.8 MiB |

## How to reproduce

```powershell
docker compose up -d
cd perf
npm install
npm run pipeline
```

Environment knobs: `PERF_SOAK_DURATION_MIN`, `PERF_PLAYER_POOL`, `PERF_K6_VUS_STEPS`, `PERF_UI_RUNS`.

Raw artifacts: `perf/reports/raw/*.json`.
