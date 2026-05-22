import { execSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  createNpc,
  getSessionLive,
  getSessionVersion,
  resolveAction,
  submitPlayerAction,
  type PerfFixture,
} from '../lib/api.js';
import { ensureReportDirs, perfConfig, uniqueLabel } from '../lib/config.js';

interface MemorySample {
  timestamp: string;
  elapsedSec: number;
  containers: Array<{ name: string; memUsageBytes: number; memLimitBytes: number; cpuPercent: number }>;
}

interface SoakSample {
  timestamp: string;
  elapsedSec: number;
  actionCount: number;
  sessionVersion: number;
  liveFetchMs: number;
  livePayloadBytes: number;
  actionSubmitMs?: number;
  actionResolveMs?: number;
}

function readFixture(): PerfFixture {
  const fixturePath = join(perfConfig.rawDir, 'fixture.json');
  if (!existsSync(fixturePath)) {
    throw new Error(`Missing ${fixturePath}. Run npm run bootstrap first.`);
  }
  return JSON.parse(readFileSync(fixturePath, 'utf8')) as PerfFixture;
}

function sampleDockerMemory(): MemorySample['containers'] {
  try {
    const output = execSync(
      'docker stats --no-stream --format "{{.Name}}\\t{{.MemUsage}}\\t{{.CPUPerc}}"',
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] },
    );

    return output
      .trim()
      .split('\n')
      .filter(Boolean)
      .map(line => {
        const [name, memUsage, cpuPercent] = line.split('\t');
        const [usedRaw, limitRaw] = (memUsage || '').split(' / ');
        return {
          name,
          memUsageBytes: parseDockerBytes(usedRaw),
          memLimitBytes: parseDockerBytes(limitRaw),
          cpuPercent: Number.parseFloat((cpuPercent || '0').replace('%', '')) || 0,
        };
      })
      .filter(row => /api|ui|mobile|postgres|redis/i.test(row.name));
  } catch {
    return [];
  }
}

function parseDockerBytes(value?: string) {
  if (!value) return 0;
  const trimmed = value.trim();
  const match = trimmed.match(/^([\d.]+)\s*(KiB|MiB|GiB|B)?$/i);
  if (!match) return 0;
  const amount = Number.parseFloat(match[1]);
  const unit = (match[2] || 'B').toUpperCase();
  if (unit === 'GIB') return amount * 1024 ** 3;
  if (unit === 'MIB') return amount * 1024 ** 2;
  if (unit === 'KIB') return amount * 1024;
  return amount;
}

function sleep(ms: number) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function main() {
  ensureReportDirs();
  const fixture = readFixture();
  const startedAt = Date.now();
  const endAt = startedAt + perfConfig.soakDurationMin * 60_000;

  console.log(`Starting ${perfConfig.soakDurationMin} min soak on session ${fixture.sessionId}`);

  const memorySamples: MemorySample[] = [];
  const soakSamples: SoakSample[] = [];
  let actionCount = 0;
  let sinceSequence = 0;
  let nextMemorySample = startedAt;
  let playerIndex = 0;

  while (Date.now() < endAt) {
    const now = Date.now();
    const elapsedSec = Math.round((now - startedAt) / 1000);

    if (now >= nextMemorySample) {
      memorySamples.push({
        timestamp: new Date(now).toISOString(),
        elapsedSec,
        containers: sampleDockerMemory(),
      });
      nextMemorySample = now + perfConfig.memorySampleIntervalSec * 1000;
    }

    const player = fixture.players[playerIndex % fixture.players.length];
    playerIndex += 1;

    const submitStarted = performance.now();
    const submitted = await submitPlayerAction(
      perfConfig.apiUrl,
      fixture.joinCode,
      player.token,
      `Soak action #${actionCount + 1} from ${player.characterName}`,
    );
    const actionSubmitMs = performance.now() - submitStarted;

    const resolveStarted = performance.now();
    await resolveAction(
      perfConfig.apiUrl,
      fixture.dmToken,
      submitted.id,
      `Resolved soak action #${actionCount + 1}`,
    );
    const actionResolveMs = performance.now() - resolveStarted;
    actionCount += 1;

    const liveStarted = performance.now();
    const version = await getSessionVersion(perfConfig.apiUrl, fixture.sessionId, fixture.dmToken);
    const live = await getSessionLive(
      perfConfig.apiUrl,
      fixture.sessionId,
      fixture.dmToken,
      sinceSequence,
    ) as { actions?: Array<{ sequence?: number }> };
    const liveFetchMs = performance.now() - liveStarted;
    const livePayloadBytes = Buffer.byteLength(JSON.stringify(live), 'utf8');
    const maxSequence = Math.max(
      sinceSequence,
      ...(live.actions ?? []).map(action => action.sequence ?? 0),
    );
    sinceSequence = maxSequence;

    soakSamples.push({
      timestamp: new Date().toISOString(),
      elapsedSec,
      actionCount,
      sessionVersion: version.version,
      liveFetchMs,
      livePayloadBytes,
      actionSubmitMs,
      actionResolveMs,
    });

    if (actionCount % 20 === 0) {
      await createNpc(perfConfig.apiUrl, fixture.dmToken, fixture.gameId, uniqueLabel('Soak NPC'));
      console.log(`  ${actionCount} actions, version=${version.version}, live=${livePayloadBytes} bytes`);
    }

    await sleep(perfConfig.soakActionIntervalMs);
  }

  const apiContainer = memorySamples.flatMap(sample => sample.containers.filter(c => /api/i.test(c.name)));
  const peakApiMem = apiContainer.reduce((max, row) => Math.max(max, row.memUsageBytes), 0);
  const firstApiMem = apiContainer[0]?.memUsageBytes ?? 0;
  const lastApiMem = apiContainer[apiContainer.length - 1]?.memUsageBytes ?? 0;

  const result = {
    generatedAt: new Date().toISOString(),
    durationMin: perfConfig.soakDurationMin,
    sessionId: fixture.sessionId,
    joinCode: fixture.joinCode,
    totalActions: actionCount,
    memorySamples,
    soakSamples,
    summary: {
      peakApiMemBytes: peakApiMem,
      apiMemDeltaBytes: lastApiMem - firstApiMem,
      avgLiveFetchMs: soakSamples.reduce((sum, row) => sum + row.liveFetchMs, 0) / Math.max(soakSamples.length, 1),
      maxLivePayloadBytes: Math.max(...soakSamples.map(row => row.livePayloadBytes), 0),
      finalSessionVersion: soakSamples[soakSamples.length - 1]?.sessionVersion ?? 0,
    },
  };

  writeFileSync(join(perfConfig.rawDir, 'soak-results.json'), JSON.stringify(result, null, 2));
  console.log(`Soak complete — ${actionCount} actions, peak API memory sample ${peakApiMem} bytes`);
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
