import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { perfConfig } from './config.js';
import {
  formatBytes,
  formatMs,
  formatPercent,
  formatRate,
  readK6Summary,
  round,
  type NumericSummary,
} from './metrics.js';

interface SteppedRow {
  vus: number;
  rps: number;
  errorRate: number;
  p50Ms: number;
  p95Ms: number;
  p99Ms: number;
  avgMs: number;
  failedRun?: boolean;
}

interface SteppedModeResult {
  mode: string;
  label: string;
  rows: SteppedRow[];
  degradation: SteppedRow | null;
  failure: SteppedRow | null;
  maxStableVus: number;
  peakRps: number;
}

function readJson<T>(path: string): T | null {
  if (!existsSync(path)) return null;
  return JSON.parse(readFileSync(path, 'utf8')) as T;
}

function table(headers: string[], rows: string[][]) {
  const headerLine = `| ${headers.join(' | ')} |`;
  const divider = `| ${headers.map(() => '---').join(' | ')} |`;
  const body = rows.map(row => `| ${row.join(' | ')} |`).join('\n');
  return [headerLine, divider, body].join('\n');
}

function fmtSummary(summary: NumericSummary) {
  return `p50 ${formatMs(summary.p50)}, p95 ${formatMs(summary.p95)}, p99 ${formatMs(summary.p99)}`;
}

function buildSteppedSection(stepped: { steppedResults: SteppedModeResult[]; maxUsers: SteppedRow | null } | null) {
  if (!stepped?.steppedResults?.length) {
    return '_No k6 stepped load data found. Run `npm run test:k6` after bootstrap._\n';
  }

  const sections = stepped.steppedResults.map(modeResult => {
    const rows = modeResult.rows.map(row => [
      String(row.vus),
      formatRate(row.rps),
      formatPercent(row.errorRate),
      formatMs(row.p95Ms),
      formatMs(row.p99Ms),
      row.failedRun ? 'yes' : 'no',
    ]);

    const degrade = modeResult.degradation
      ? `${modeResult.degradation.vus} VUs (${formatPercent(modeResult.degradation.errorRate)} errors, p95 ${formatMs(modeResult.degradation.p95Ms)})`
      : 'Not reached in tested range';
    const fail = modeResult.failure
      ? `${modeResult.failure.vus} VUs (${formatPercent(modeResult.failure.errorRate)} errors)`
      : 'Not reached in tested range';

    return [
      `### ${modeResult.label}`,
      '',
      table(['VUs', 'Throughput', 'Error rate', 'p95', 'p99', 'Run failed'], rows),
      '',
      `- **Max stable VUs** (below degradation thresholds): **${modeResult.maxStableVus || 'unknown'}**`,
      `- **Peak throughput observed**: **${formatRate(modeResult.peakRps)}**`,
      `- **Degradation begins**: ${degrade}`,
      `- **Failure threshold crossed**: ${fail}`,
      '',
    ].join('\n');
  });

  const ramp = stepped.maxUsers
    ? `- Ramp scenario peak: ${formatRate(stepped.maxUsers.rps)} @ p95 ${formatMs(stepped.maxUsers.p95Ms)}, error rate ${formatPercent(stepped.maxUsers.errorRate)}`
    : '- Ramp scenario: no summary captured';

  return [
    '## API load & throughput',
    '',
    'Stepped k6 probes increase virtual users until error rate or latency thresholds are crossed.',
    '',
    ...sections,
    '### Concurrent user ramp',
    '',
    ramp,
    '',
  ].join('\n');
}

function buildSoakSection(soak: {
  durationMin: number;
  totalActions: number;
  summary: {
    peakApiMemBytes: number;
    apiMemDeltaBytes: number;
    avgLiveFetchMs: number;
    maxLivePayloadBytes: number;
    finalSessionVersion: number;
  };
  memorySamples: Array<{ elapsedSec: number; containers: Array<{ name: string; memUsageBytes: number }> }>;
} | null) {
  if (!soak) {
    return '_No soak test data found. Run `npm run soak` after bootstrap._\n';
  }

  const memoryRows = soak.memorySamples
    .flatMap(sample => sample.containers.map(container => ({
      elapsedSec: sample.elapsedSec,
      name: container.name,
      memUsageBytes: container.memUsageBytes,
    })))
    .filter((row, index, all) => index < 12 || index === all.length - 1)
    .slice(0, 15)
    .map(row => [String(row.elapsedSec), row.name, formatBytes(row.memUsageBytes)]);

  return [
    '## Long-running session & memory',
    '',
    `- **Soak duration**: ${soak.durationMin} min`,
    `- **Actions submitted & resolved**: ${soak.totalActions}`,
    `- **Final session version**: ${soak.summary.finalSessionVersion}`,
    `- **Average live fetch latency**: ${formatMs(soak.summary.avgLiveFetchMs)}`,
    `- **Largest incremental live payload**: ${formatBytes(soak.summary.maxLivePayloadBytes)}`,
    `- **Peak API container memory (sampled)**: ${formatBytes(soak.summary.peakApiMemBytes)}`,
    `- **API memory delta (first → last sample)**: ${formatBytes(soak.summary.apiMemDeltaBytes)}`,
    '',
    table(['Elapsed (s)', 'Container', 'Memory'], memoryRows),
    '',
  ].join('\n');
}

function buildUiSection(ui: {
  runs: number;
  pages: Record<string, Record<string, NumericSummary>>;
  interactions: Record<string, NumericSummary>;
} | null) {
  if (!ui) {
    return '_No UI latency data found. Run `npm run test:ui` after bootstrap._\n';
  }

  const pageRows = Object.entries(ui.pages).map(([name, metrics]) => [
    name,
    fmtSummary(metrics.ttfb),
    fmtSummary(metrics.domContentLoaded),
    fmtSummary(metrics.loadComplete),
  ]);

  const interactionRows = Object.entries(ui.interactions).map(([name, metrics]) => [
    name,
    formatMs(metrics.p50),
    formatMs(metrics.p95),
    formatMs(metrics.p99),
  ]);

  return [
    '## UI latency (primary)',
    '',
    `Measured with Playwright over **${ui.runs}** runs per page (production UI @ ${perfConfig.uiUrl}).`,
    '',
    '### Page load',
    '',
    table(['Page', 'TTFB', 'DOMContentLoaded', 'Load complete'], pageRows),
    '',
    '### Interaction latency',
    '',
    table(['Interaction', 'p50', 'p95', 'p99'], interactionRows),
    '',
    '**Notes**',
    '- TTFB includes Nuxt SSR/BFF proxy time to the API.',
    '- Player session metrics assume bootstrap fixture and seeded player token.',
    '',
  ].join('\n');
}

function buildExecutiveSummary(
  stepped: { steppedResults: SteppedModeResult[] } | null,
  soak: { totalActions: number; summary: { peakApiMemBytes: number; avgLiveFetchMs: number } } | null,
  ui: { pages: Record<string, { total: NumericSummary }> } | null,
  health: ReturnType<typeof readK6Summary> | null,
) {
  const mixed = stepped?.steppedResults.find(result => result.mode === 'mixed');
  const actions = stepped?.steppedResults.find(result => result.mode === 'action-submit');
  const player = stepped?.steppedResults.find(result => result.mode === 'player-poll');

  const uiPlayer = ui?.pages.player_session?.total;
  const uiDm = ui?.pages.dm_session?.total;

  return [
    '## Executive summary',
    '',
    '| Metric | Value |',
    '| --- | --- |',
    `| Baseline health p95 | ${health ? formatMs(health.p95Ms) : 'n/a'} |`,
    `| Mixed polling — max stable VUs | **${mixed?.maxStableVus ?? 'n/a'}** |`,
    `| Mixed polling — peak throughput | **${mixed ? formatRate(mixed.peakRps) : 'n/a'}** |`,
    `| Player polling — max stable VUs | **${player?.maxStableVus ?? 'n/a'}** |`,
    `| Action submit — max stable VUs | **${actions?.maxStableVus ?? 'n/a'}** |`,
    `| Action submit — peak throughput | **${actions ? formatRate(actions.peakRps) : 'n/a'}** |`,
    `| Degradation threshold | error ≥ ${formatPercent(perfConfig.k6DegradeErrorRate)} or p95 ≥ ${formatMs(perfConfig.k6DegradeP95Ms)} |`,
    `| Failure threshold | error ≥ ${formatPercent(perfConfig.k6FailErrorRate)} |`,
    `| Soak actions resolved | ${soak?.totalActions ?? 'n/a'} |`,
    `| Peak API memory (soak) | ${soak ? formatBytes(soak.summary.peakApiMemBytes) : 'n/a'} |`,
    `| UI player session load p95 | ${uiPlayer ? formatMs(uiPlayer.p95) : 'n/a'} |`,
    `| UI DM session load p95 | ${uiDm ? formatMs(uiDm.p95) : 'n/a'} |`,
    '',
  ].join('\n');
}

function main() {
  mkdirSync(perfConfig.rawDir, { recursive: true });

  const stepped = readJson<{ steppedResults: SteppedModeResult[]; maxUsers: SteppedRow | null }>(
    join(perfConfig.rawDir, 'k6-stepped-summary.json'),
  );
  const soak = readJson<{
    durationMin: number;
    totalActions: number;
    summary: {
      peakApiMemBytes: number;
      apiMemDeltaBytes: number;
      avgLiveFetchMs: number;
      maxLivePayloadBytes: number;
      finalSessionVersion: number;
    };
    memorySamples: Array<{ elapsedSec: number; containers: Array<{ name: string; memUsageBytes: number }> }>;
  }>(join(perfConfig.rawDir, 'soak-results.json'));
  const ui = readJson<{
    runs: number;
    pages: Record<string, Record<string, NumericSummary>>;
    interactions: Record<string, NumericSummary>;
  }>(join(perfConfig.rawDir, 'ui-latency.json'));
  const healthRaw = readJson<{ metrics?: Record<string, unknown> }>(
    join(perfConfig.rawDir, 'k6-health-baseline.json'),
  );
  const health = healthRaw ? readK6Summary(healthRaw) : null;
  const fixture = readJson<{ joinCode: string; sessionId: string; players: unknown[]; createdAt: string }>(
    join(perfConfig.rawDir, 'fixture.json'),
  );

  const report = [
    '# TTRPG Table — Performance Report',
    '',
    `- **Generated**: ${new Date().toISOString()}`,
    `- **API target**: ${perfConfig.apiUrl}`,
    `- **UI target**: ${perfConfig.uiUrl}`,
    `- **Fixture session**: ${fixture?.sessionId ?? 'n/a'} (${fixture?.players?.length ?? 0} pre-joined players)`,
    `- **Degradation thresholds**: error ≥ ${formatPercent(perfConfig.k6DegradeErrorRate)}, p95 ≥ ${formatMs(perfConfig.k6DegradeP95Ms)}`,
    '',
    buildExecutiveSummary(stepped, soak, ui, health),
    buildUiSection(ui),
    buildSteppedSection(stepped),
    buildSoakSection(soak),
    '## How to reproduce',
    '',
    '```powershell',
    'docker compose up -d',
    'cd perf',
    'npm install',
    'npm run pipeline',
    '```',
    '',
    'Environment knobs: `PERF_SOAK_DURATION_MIN`, `PERF_PLAYER_POOL`, `PERF_K6_VUS_STEPS`, `PERF_UI_RUNS`.',
    '',
    'Raw artifacts: `perf/reports/raw/*.json`.',
    '',
  ].join('\n');

  writeFileSync(perfConfig.reportPath, report);
  console.log(`Wrote ${perfConfig.reportPath}`);
}

main();
