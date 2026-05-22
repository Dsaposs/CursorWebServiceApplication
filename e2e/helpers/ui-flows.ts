import { expect, type Page } from '@playwright/test';
import { readAdminAuthCache } from './auth-cache';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, scientistCharacterBuild } from './test-data';

async function addSkillPoints(page: Page, skillLabel: string, points: number) {
  const row = page.locator('.stat-delta-row', { hasText: skillLabel });
  const plusButton = row.getByRole('button', { name: '+' });
  for (let i = 0; i < points; i += 1) {
    await plusButton.click();
  }
}

export async function allocateScientistSkillPoints(page: Page) {
  const allocations: Record<string, number> = scientistCharacterBuild.skillAllocations;
  await addSkillPoints(page, 'Observation', allocations.observation);
  await addSkillPoints(page, 'Survival', allocations.survival);
  await addSkillPoints(page, 'Comtech', allocations.comtech);
  await addSkillPoints(page, 'Medical Aid', allocations.medicalAid);
  await expect(page.getByText('10 / 10 spent')).toBeVisible();
}

export async function seedAdminAuth(page: Page, email = E2E_ADMIN_EMAIL, token?: string) {
  const cached = readAdminAuthCache();
  const authToken = token ?? cached?.token;
  if (!authToken) {
    throw new Error('Admin auth token missing. Global setup should seed e2e/.auth/admin-token.json.');
  }

  await page.addInitScript(({ storedToken, storedEmail }) => {
    localStorage.setItem('ttrpg_token', storedToken);
    localStorage.setItem('ttrpg_email', storedEmail);
  }, {
    storedToken: authToken,
    storedEmail: email,
  });
}

export async function loginThroughUi(page: Page, email = E2E_ADMIN_EMAIL, password = E2E_ADMIN_PASSWORD) {
  await page.goto('/login');
  await page.locator('form').getByRole('button', { name: 'Sign In' }).click();
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.locator('form').getByRole('button', { name: 'Sign In' }).click();
  await expect(page).toHaveURL(/\/games$/);
}

export async function createGameAndStartSession(page: Page, gameName: string) {
  await page.getByRole('button', { name: '+ New' }).click();
  await page.getByLabel('Game name').fill(gameName);
  await page.getByLabel('Ruleset').selectOption('alien-rpg');
  await page.getByRole('button', { name: 'Create Game' }).click();
  await expect(page.getByRole('heading', { name: gameName, level: 1 })).toBeVisible();
  await page.getByRole('button', { name: '+ Start' }).click();
  await expect(page).toHaveURL(/\/sessions\/[0-9a-f-]+\/dm$/);
}

export async function joinSessionAsNewCharacter(
  page: Page,
  joinCode: string,
  characterName: string,
  playerName: string,
) {
  await page.goto(`/join/${joinCode}`);
  await expect(page.getByRole('heading', { name: 'Join Session' })).toBeVisible();
  await page.getByRole('button', { name: 'Create New' }).click();
  await page.getByLabel('New character name').fill(characterName);
  await page.getByLabel('Class / Career').selectOption('scientist');
  await page.getByLabel(/Your name/).fill(playerName);
  await allocateScientistSkillPoints(page);

  await page.getByRole('button', { name: 'Join Session' }).click();
  await expect(page).toHaveURL(new RegExp(`/sessions/${joinCode}/player$`));
}

export async function submitExplorationAction(page: Page, actionLabel: string) {
  await page.getByRole('button', { name: 'Take Action' }).click();
  const actionSelect = page.locator('select').filter({
    has: page.locator('option', { hasText: actionLabel }),
  });
  await actionSelect.selectOption({ label: actionLabel });
  await page.getByRole('button', { name: 'Submit Action' }).click();
  await expect(page.getByText(/pending DM review/i)).toBeVisible({ timeout: 45_000 });
}

export async function dmExpandPendingActions(page: Page) {
  await expect(page.getByRole('heading', { name: /Pending Actions/i })).toBeVisible();
  await page.getByRole('button', { name: 'Expand' }).click();
}

export async function dmPromptActorRoll(page: Page) {
  await page.getByRole('button', { name: /^Prompt / }).click();
}

export async function playerSubmitAutoRoll(page: Page) {
  await page.getByRole('button', { name: /Roll \d+d\d+/i }).click({ timeout: 45_000 });
  await page.getByRole('button', { name: 'Submit roll to DM' }).click();
}

export async function dmPublishResolution(page: Page, note: string) {
  await page.getByLabel('Resolution note (optional)').fill(note);
  const publishButton = page.getByRole('button', { name: 'Publish Resolution' });
  await expect(publishButton).toBeEnabled({ timeout: 45_000 });
  await publishButton.click();
  await expect(page.getByText(/Resolution published to players|published to players/i)).toBeVisible({ timeout: 45_000 });
}

export async function registerThroughUi(page: Page, email: string, password: string) {
  await page.goto('/login');
  await page.getByRole('button', { name: 'Register' }).click();
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm password').fill(password);
  await page.getByRole('button', { name: 'Create Account & Sign In' }).click();
  await expect(page).toHaveURL(/\/games$/);
}

export async function openGameNpcTab(page: Page, gameName: string) {
  await page.goto('/games');
  await page.getByRole('button', { name: gameName }).click();
  await page.getByRole('button', { name: 'NPCs / Monsters' }).click();
  await expect(page.getByRole('heading', { name: /Add NPC \/ Monster|Edit NPC \/ Monster/ })).toBeVisible();
}

export async function addNpcViaGamesHub(page: Page, npcName: string) {
  await page.getByLabel('Name').fill(npcName);
  await page.getByLabel('Max HP').fill('10');
  await page.getByLabel('Current HP').fill('10');
  await page.locator('.npc-form-panel form').evaluate((form: HTMLFormElement) => form.requestSubmit());
  await expect(page.locator('.npc-card-name').filter({ hasText: npcName })).toBeVisible({ timeout: 45_000 });
}

export async function addNpcViaDmScreen(page: Page, npcName: string) {
  await page.getByRole('button', { name: 'Add NPC' }).click();
  await page.getByLabel('Name').fill(npcName);
  await page.getByLabel('Max HP').fill('10');
  await page.getByLabel('Current HP').fill('10');
  await page.locator('.dm-npc-form').evaluate((form: HTMLFormElement) => form.requestSubmit());
  await expect(page.locator('strong').filter({ hasText: npcName })).toBeVisible({ timeout: 45_000 });
}

export async function dmRequestStatCheck(
  page: Page,
  characterName: string,
  statLabel: string,
  note?: string,
) {
  await page.getByRole('button', { name: 'Request Stat Check' }).click();
  await page.locator('.follow-up-roll-player', { hasText: characterName }).locator('input[type="checkbox"]').check();
  await page.getByLabel('Stat').selectOption({ label: statLabel });
  if (note) {
    await page.getByLabel(/Note for players/).fill(note);
  }
  await page.getByRole('button', { name: /Send to \d+ player/ }).click();
  await expect(page.getByText('Awaiting roll')).toBeVisible({ timeout: 45_000 });
}

export async function seedPlayerToken(page: Page, joinCode: string, token: string, gameId: string) {
  await page.addInitScript(({ sessionKey, gameKey, value }) => {
    localStorage.setItem(sessionKey, value);
    localStorage.setItem(gameKey, value);
  }, {
    sessionKey: `ttrpg_player_${joinCode}`,
    gameKey: `ttrpg_player_${gameId}`,
    value: token,
  });
}
