import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import {
  addNpcViaDmScreen,
  addNpcViaGamesHub,
  openGameNpcTab,
  seedAdminAuth,
} from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('NPC management', () => {
  test('DM can add an NPC from the games hub using a template', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const npcName = uniqueLabel('E2E Drone');

    await seedAdminAuth(page);
    await openGameNpcTab(page, fixture.gameName);
    await addNpcViaGamesHub(page, npcName);

    await expect(page.getByText(/HP \d+\/\d+/).first()).toBeVisible();
  });

  test('DM can edit and delete an NPC from the games hub', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const npcName = uniqueLabel('E2E Guard');
    const updatedName = `${npcName}-edited`;

    await seedAdminAuth(page);
    await openGameNpcTab(page, fixture.gameName);
    await addNpcViaGamesHub(page, npcName);

    await page.getByRole('button', { name: 'Edit' }).first().click();
    await page.getByLabel('Name').fill(updatedName);
    await page.getByRole('button', { name: 'Save Changes' }).click();
    await expect(page.getByText(updatedName)).toBeVisible();

    await page.getByRole('button', { name: 'Delete NPC' }).first().click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete NPC' }).click();
    await expect(page.getByRole('dialog')).not.toBeVisible();
    await expect(page.locator('.npc-card-name').filter({ hasText: updatedName })).not.toBeVisible();
  });

  test('DM can add an NPC during a live session', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const npcName = uniqueLabel('E2E Session NPC');

    await seedAdminAuth(page);
    await page.goto(`/sessions/${fixture.sessionId}/dm`);
    await addNpcViaDmScreen(page, npcName);
  });

  test('API create and delete NPC updates game roster', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });
    const npcName = uniqueLabel('API Xenomorph');

    const created = await client.createNpc(fixture.gameId, {
      templateKey: 'xenomorphDrone',
      name: npcName,
    });
    expect(created.name).toBe(npcName);

    const game = await client.getGame(fixture.gameId);
    expect(game.npcsAndMonsters.some(npc => npc.id === created.id)).toBeTruthy();

    await client.deleteNpc(fixture.gameId, created.id);
    const afterDelete = await client.getGame(fixture.gameId);
    expect(afterDelete.npcsAndMonsters.some(npc => npc.id === created.id)).toBeFalsy();
  });
});
