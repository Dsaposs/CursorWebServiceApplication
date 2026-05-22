import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { perfConfig, ensureReportDirs } from '../lib/config.js';
import { findDegradationPoint, findFailurePoint, readK6Summary } from '../lib/metrics.js';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');
const k6Dir = join(rootDir, 'k6');
const fixturePath = join(perfConfig.rawDir, 'fixture.json');

interface K6Runner {
  command: string;
  docker: boolean;
  prefix: string[];
}

function resolveK6Runner(): K6Runner {
  if (process.env.K6_BIN) {
    return { command: process.env.K6_BIN, docker: false, prefix: [] };
  }

  const dockerCheck = spawnSync('docker', ['--version'], { encoding: 'utf8' });
  if (dockerCheck.status === 0) {
    return {
      command: 'docker',
      docker: true,
      prefix: [
        'run', '--rm', '-i',
        '--add-host=host.docker.internal:host-gateway',
        '-v', `${rootDir}:/perf`,
        '-w', '/perf/k6',
        'grafana/k6',
      ],
    };
  }

  return { command: 'k6', docker: false, prefix: [] };
}

function mapApiUrlForRunner(apiUrl: string, docker: boolean) {
  if (!docker) return apiUrl;
  return apiUrl
    .replace('localhost', 'host.docker.internal')
    .replace('127.0.0.1', 'host.docker.internal');
}

function runK6(scriptName: string, env: Record<string, string>, runner: K6Runner) {
  const apiUrl = mapApiUrlForRunner(env.API_URL || perfConfig.apiUrl, runner.docker);
  const envArgs = Object.entries({ ...env, API_URL: apiUrl })
    .flatMap(([key, value]) => ['-e', `${key}=${value}`]);

  const args = [
    ...runner.prefix,
    'run',
    '--no-thresholds',
    ...envArgs,
    ...(runner.docker ? [scriptName] : [join(k6Dir, scriptName)]),
  ];

  console.log(`> ${runner.command} ${args.join(' ')}`);
  const result = spawnSync(runner.command, args, {
    cwd: runner.docker ? rootDir : k6Dir,
    stdio: 'inherit',
    shell: process.platform === 'win32',
  });

  const outputSlug = env.OUTPUT_SLUG;
  const summaryExists = outputSlug
    ? existsSync(join(perfConfig.rawDir, `${outputSlug}.json`))
    : existsSync(join(perfConfig.rawDir, 'k6-max-users-ramp.json'));

  if (result.status !== 0 && !summaryExists) {
    throw new Error(`k6 failed for ${scriptName} (exit ${result.status ?? 'unknown'})`);
  }

  if (result.status !== 0) {
    console.warn(`k6 exited ${result.status} for ${scriptName}, using captured summary.`);
  }
}

function loadSummary(path: string) {
  if (!existsSync(path)) return null;
  return JSON.parse(readFileSync(path, 'utf8'));
}

function main() {
  ensureReportDirs();

  if (!existsSync(fixturePath)) {
    throw new Error(`Missing ${fixturePath}. Run npm run bootstrap first.`);
  }

  const runner = resolveK6Runner();
  if (runner.docker) {
    console.log('Using k6 via Docker (grafana/k6)');
  }

  runK6('01-health-baseline.js', {
    VUS: '10',
    DURATION: `${perfConfig.k6StepDurationSec}s`,
    OUTPUT_SLUG: 'k6-health-baseline',
  }, runner);

  const steppedModes = [
    { mode: 'mixed', label: 'Mixed poll (player + DM)' },
    { mode: 'player-poll', label: 'Player polling' },
    { mode: 'action-submit', label: 'Action submit throughput' },
  ];

  const steppedResults = [];

  for (const { mode, label } of steppedModes) {
    const modeRows = [];

    for (const vus of perfConfig.k6VusSteps) {
      const slug = `k6-stepped-${mode}-${vus}`;
      try {
        runK6('02-stepped-probe.js', {
          MODE: mode,
          VUS: String(vus),
          DURATION: `${perfConfig.k6StepDurationSec}s`,
          OUTPUT_SLUG: slug,
          SLEEP_SEC: mode === 'action-submit' ? '0.15' : '0.4',
        }, runner);
      } catch {
        modeRows.push({
          mode,
          label,
          vus,
          rps: 0,
          errorRate: 1,
          p50Ms: 0,
          p95Ms: 0,
          p99Ms: 0,
          avgMs: 0,
          maxMs: 0,
          checksPass: 0,
          checksFail: 0,
          failedRun: true,
        });
        break;
      }

      const summaryPath = join(perfConfig.rawDir, `${slug}.json`);
      const summary = loadSummary(summaryPath);
      if (!summary) {
        modeRows.push({
          mode,
          label,
          vus,
          rps: 0,
          errorRate: 1,
          p50Ms: 0,
          p95Ms: 0,
          p99Ms: 0,
          avgMs: 0,
          maxMs: 0,
          checksPass: 0,
          checksFail: 0,
          failedRun: true,
        });
        break;
      }

      const parsed = readK6Summary(summary);
      modeRows.push({ mode, label, vus, ...parsed, failedRun: false });

      if (parsed.errorRate >= perfConfig.k6FailErrorRate) {
        console.warn(`Stopping ${mode} at ${vus} VUs — error rate ${(parsed.errorRate * 100).toFixed(2)}%`);
        break;
      }
    }

    steppedResults.push({
      mode,
      label,
      rows: modeRows,
      degradation: findDegradationPoint(modeRows, perfConfig.k6DegradeErrorRate, perfConfig.k6DegradeP95Ms),
      failure: findFailurePoint(modeRows, perfConfig.k6FailErrorRate),
      maxStableVus: [...modeRows].reverse().find(row => !row.failedRun && row.errorRate < perfConfig.k6DegradeErrorRate)?.vus ?? 0,
      peakRps: Math.max(...modeRows.map(row => row.rps), 0),
    });
  }

  runK6('03-max-users-ramp.js', {}, runner);

  const maxUsersSummary = loadSummary(join(perfConfig.rawDir, 'k6-max-users-ramp.json'));
  const maxUsers = maxUsersSummary ? readK6Summary(maxUsersSummary) : null;

  writeFileSync(
    join(perfConfig.rawDir, 'k6-stepped-summary.json'),
    JSON.stringify({ generatedAt: new Date().toISOString(), steppedResults, maxUsers }, null, 2),
  );

  console.log('k6 suite complete.');
}

main();
