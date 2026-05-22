import { test, expect } from '@playwright/test';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { perfConfig } from '../lib/config.js';
import { summarize } from '../lib/metrics.js';

const rawDir = join(process.cwd(), 'reports', 'raw');
const fixturePath = join(rawDir, 'fixture.json');
const runs = perfConfig.uiLatencyRuns;

interface NavigationMetrics {
  ttfb: number;
  domContentLoaded: number;
  loadComplete: number;
  total: number;
}

interface InteractionMetrics {
  name: string;
  durationMs: number;
}

const pageResults: Record<string, NavigationMetrics[]> = {};
const interactionResults: Record<string, number[]> = {};

function recordPage(name: string, metrics: NavigationMetrics) {
  pageResults[name] ??= [];
  pageResults[name].push(metrics);
}

function recordInteraction(name: string, durationMs: number) {
  interactionResults[name] ??= [];
  interactionResults[name].push(durationMs);
}

async function readNavigationMetrics(page: import('@playwright/test').Page): Promise<NavigationMetrics> {
  return page.evaluate(() => {
    const nav = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
    if (!nav) {
      return { ttfb: 0, domContentLoaded: 0, loadComplete: 0, total: 0 };
    }
    return {
      ttfb: nav.responseStart - nav.startTime,
      domContentLoaded: nav.domContentLoadedEventEnd - nav.startTime,
      loadComplete: nav.loadEventEnd - nav.startTime,
      total: nav.loadEventEnd - nav.startTime,
    };
  });
}

async function seedAdminAuth(page: import('@playwright/test').Page) {
  const authPath = join(process.cwd(), '.auth', 'admin-token.json');
  if (!existsSync(authPath)) {
    throw new Error('Missing perf/.auth/admin-token.json — global setup should create it.');
  }
  const auth = JSON.parse(readFileSync(authPath, 'utf8')) as { token: string; email: string };
  await page.addInitScript(({ storedToken, storedEmail }) => {
    localStorage.setItem('ttrpg_token', storedToken);
    localStorage.setItem('ttrpg_email', storedEmail);
  }, { storedToken: auth.token, storedEmail: auth.email });
}

async function seedPlayerAuth(page: import('@playwright/test').Page) {
  if (!existsSync(fixturePath)) {
    throw new Error('Missing fixture.json — run bootstrap before UI latency tests.');
  }
  const fixture = JSON.parse(readFileSync(fixturePath, 'utf8')) as {
    joinCode: string;
    gameId: string;
    players: Array<{ token: string }>;
  };
  const player = fixture.players[0];
  await page.addInitScript(({ sessionKey, gameKey, value, joinCode }) => {
    localStorage.setItem(sessionKey, value);
    localStorage.setItem(gameKey, value);
    localStorage.setItem(`ttrpg_player_${joinCode}`, value);
  }, {
    sessionKey: `ttrpg_player_${fixture.joinCode}`,
    gameKey: `ttrpg_player_${fixture.gameId}`,
    value: player.token,
    joinCode: fixture.joinCode,
  });
}

test.describe('UI latency', () => {
  test(`collect navigation and interaction timings (${runs} runs)`, async ({ page }) => {
    mkdirSync(rawDir, { recursive: true });

    for (let run = 0; run < runs; run += 1) {
      await page.goto('/login', { waitUntil: 'networkidle' });
      recordPage('login', await readNavigationMetrics(page));

      await seedAdminAuth(page);
      await page.goto('/games', { waitUntil: 'networkidle' });
      recordPage('games_hub', await readNavigationMetrics(page));

      if (existsSync(fixturePath)) {
        const fixture = JSON.parse(readFileSync(fixturePath, 'utf8')) as { sessionId: string; joinCode: string };
        await page.goto(`/sessions/${fixture.sessionId}/dm`, { waitUntil: 'networkidle' });
        recordPage('dm_session', await readNavigationMetrics(page));

        const expandStarted = Date.now();
        const expandButton = page.getByRole('button', { name: 'Expand' });
        if (await expandButton.count()) {
          await expandButton.first().click();
        }
        recordInteraction('dm_expand_pending_actions', Date.now() - expandStarted);

        await page.context().clearCookies();
        await page.evaluate(() => localStorage.clear());
        await seedPlayerAuth(page);
        await page.goto(`/sessions/${fixture.joinCode}/player`, { waitUntil: 'networkidle' });
        recordPage('player_session', await readNavigationMetrics(page));

        const actionStarted = Date.now();
        const takeAction = page.getByRole('button', { name: 'Take Action' });
        if (await takeAction.count()) {
          await takeAction.click();
          await expect(page.getByRole('button', { name: 'Submit Action' })).toBeVisible();
        }
        recordInteraction('player_open_action_form', Date.now() - actionStarted);
      }
    }

    const output = {
      generatedAt: new Date().toISOString(),
      runs,
      pages: Object.fromEntries(
        Object.entries(pageResults).map(([name, samples]) => [
          name,
          {
            ttfb: summarize(samples.map(sample => sample.ttfb)),
            domContentLoaded: summarize(samples.map(sample => sample.domContentLoaded)),
            loadComplete: summarize(samples.map(sample => sample.loadComplete)),
            total: summarize(samples.map(sample => sample.total)),
          },
        ]),
      ),
      interactions: Object.fromEntries(
        Object.entries(interactionResults).map(([name, samples]) => [name, summarize(samples)]),
      ),
    };

    writeFileSync(join(rawDir, 'ui-latency.json'), JSON.stringify(output, null, 2));
  });
});
