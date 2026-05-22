import { mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');

export const perfConfig = {
  apiUrl: process.env.PERF_API_URL ?? process.env.E2E_API_URL ?? 'http://localhost:5294',
  uiUrl: process.env.PERF_UI_URL ?? process.env.E2E_BASE_URL ?? 'http://localhost:3000',
  mobileUrl: process.env.PERF_MOBILE_URL ?? process.env.E2E_MOBILE_BASE_URL ?? 'http://localhost:3001',
  adminEmail: process.env.PERF_ADMIN_EMAIL ?? process.env.E2E_ADMIN_EMAIL ?? 'admin@example.local',
  adminPassword: process.env.PERF_ADMIN_PASSWORD ?? process.env.E2E_ADMIN_PASSWORD ?? 'Password1',
  playerPoolSize: Number(process.env.PERF_PLAYER_POOL ?? '100'),
  soakDurationMin: Number(process.env.PERF_SOAK_DURATION_MIN ?? '5'),
  soakActionIntervalMs: Number(process.env.PERF_SOAK_ACTION_INTERVAL_MS ?? '3000'),
  memorySampleIntervalSec: Number(process.env.PERF_MEMORY_SAMPLE_SEC ?? '30'),
  k6VusSteps: (process.env.PERF_K6_VUS_STEPS ?? '5,10,25,50,75,100,150,200')
    .split(',')
    .map(value => Number(value.trim()))
    .filter(value => Number.isFinite(value) && value > 0),
  k6StepDurationSec: Number(process.env.PERF_K6_STEP_DURATION_SEC ?? '45'),
  k6DegradeErrorRate: Number(process.env.PERF_K6_DEGRADE_ERROR_RATE ?? '0.02'),
  k6DegradeP95Ms: Number(process.env.PERF_K6_DEGRADE_P95_MS ?? '1500'),
  k6FailErrorRate: Number(process.env.PERF_K6_FAIL_ERROR_RATE ?? '0.10'),
  uiLatencyRuns: Number(process.env.PERF_UI_RUNS ?? '5'),
  reportsDir: join(rootDir, 'reports'),
  rawDir: join(rootDir, 'reports', 'raw'),
  reportPath: join(rootDir, 'reports', 'PERFORMANCE_REPORT.md'),
};

export function ensureReportDirs() {
  mkdirSync(perfConfig.rawDir, { recursive: true });
}

export const scientistCharacterBuild = {
  classKey: 'scientist',
  skillAllocations: {
    observation: 2,
    survival: 2,
    comtech: 3,
    medicalAid: 3,
  },
  startingItemKey: 'medkit',
};

export function uniqueLabel(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}
