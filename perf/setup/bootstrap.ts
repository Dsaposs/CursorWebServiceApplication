import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  createGame,
  joinSession,
  login,
  startSession,
  type PerfFixture,
} from '../lib/api.js';
import { ensureReportDirs, perfConfig, uniqueLabel } from '../lib/config.js';

async function main() {
  ensureReportDirs();

  console.log(`Bootstrapping perf fixture against ${perfConfig.apiUrl} ...`);
  const auth = await login(perfConfig.apiUrl, perfConfig.adminEmail, perfConfig.adminPassword);
  const gameName = uniqueLabel('Perf Campaign');
  const game = await createGame(perfConfig.apiUrl, auth.token, gameName);
  const session = await startSession(perfConfig.apiUrl, auth.token, game.id);

  const players = [];
  for (let i = 0; i < perfConfig.playerPoolSize; i += 1) {
    const characterName = uniqueLabel(`Player${i + 1}`);
    const joined = await joinSession(
      perfConfig.apiUrl,
      session.joinCode,
      characterName,
      `Runner ${i + 1}`,
    );
    players.push({
      token: joined.participantToken,
      characterId: joined.character.id,
      characterName,
    });

    if ((i + 1) % 25 === 0) {
      console.log(`  joined ${i + 1}/${perfConfig.playerPoolSize} players`);
    }
  }

  const fixture: PerfFixture = {
    createdAt: new Date().toISOString(),
    apiUrl: perfConfig.apiUrl,
    uiUrl: perfConfig.uiUrl,
    gameId: game.id,
    gameName,
    sessionId: session.id,
    joinCode: session.joinCode,
    dmToken: auth.token,
    players,
  };

  const fixturePath = join(perfConfig.rawDir, 'fixture.json');
  writeFileSync(fixturePath, JSON.stringify(fixture, null, 2));
  console.log(`Wrote ${fixturePath} (${players.length} players)`);
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
