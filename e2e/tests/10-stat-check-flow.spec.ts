import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import {
  dmExpandPendingActions,
  dmPublishResolution,
  dmRequestStatCheck,
  joinSessionAsNewCharacter,
  playerSubmitAutoRoll,
  seedAdminAuth,
} from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Stat-check panel', () => {
  test('DM requests a stat check, player rolls, and DM publishes the outcome', async ({ browser }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const dmClient = new ApiClient({ apiUrl, token });

    const characterName = uniqueLabel('Scout');
    const playerName = uniqueLabel('Player');
    const checkNote = uniqueLabel('Spot movement in the vents');
    const resolutionNote = uniqueLabel('You notice a flicker in the motion tracker');

    const playerContext = await browser.newContext();
    const dmContext = await browser.newContext();
    const playerPage = await playerContext.newPage();
    const dmPage = await dmContext.newPage();

    await joinSessionAsNewCharacter(playerPage, fixture.joinCode, characterName, playerName);

    await seedAdminAuth(dmPage);
    await dmPage.goto(`/sessions/${fixture.sessionId}/dm`);
    await dmRequestStatCheck(dmPage, characterName, 'Observation', checkNote);
    await playerSubmitAutoRoll(playerPage);

    await dmExpandPendingActions(dmPage);
    await expect(dmPage.locator('.badge').filter({ hasText: 'Stat check' })).toBeVisible();
    await dmPublishResolution(dmPage, resolutionNote);

    await expect.poll(async () => {
      const live = await dmClient.getSessionLive(fixture.sessionId) as {
        actions: Array<{ isSkillCheckResponse?: boolean; status: string; resolutionText?: string | null }>;
      };
      const published = live.actions.find(action => action.isSkillCheckResponse && action.status === 'Published');
      return published?.resolutionText ?? null;
    }, { timeout: 45_000 }).toBe(resolutionNote);

    // Exploration stat-check responses are not shown in the player action feed by design.
    await playerPage.reload();
    await expect(playerPage.getByRole('heading', { name: 'Your Pending Actions' })).not.toBeVisible({ timeout: 15_000 });
    await expect(playerPage.getByText('No resolved actions yet this session.')).toBeVisible();

    await playerContext.close();
    await dmContext.close();
  });

  test('API stat check prompt creates a pending action the DM can resolve', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const dmClient = new ApiClient({ apiUrl, token });

    const joined = await dmClient.joinSession(fixture.joinCode, uniqueLabel('Operator'), uniqueLabel('Player'));
    const playerClient = new ApiClient({ apiUrl, playerToken: joined.participantToken });

    const prompts = await dmClient.createSessionRollPrompts(fixture.sessionId, {
      prompts: [{
        targetCharacterId: joined.character.id,
        checkMode: 'Skill',
        skillKey: 'observation',
        promptLabel: 'Listen at the bulkhead',
        resultKind: 'PassFail',
      }],
    });

    expect(prompts.length).toBe(1);
    await playerClient.submitRollPrompt(prompts[0].id, '2 successes (rolled 9, 6)');

    const live = await dmClient.getSessionLive(fixture.sessionId) as {
      actions: Array<{ id: string; isSkillCheckResponse?: boolean; status: string }>;
    };
    const pending = live.actions.find(action => action.isSkillCheckResponse && action.status === 'Pending');
    expect(pending).toBeTruthy();

    const resolved = await dmClient.resolveAction(pending!.id, 'You hear scratching on the other side.');
    expect(resolved.status).toBe('Published');
  });

  test('DM can cancel a pending stat check before the player rolls', async ({ page }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const client = new ApiClient({ apiUrl, token });

    const joined = await client.joinSession(fixture.joinCode, uniqueLabel('Marine'), uniqueLabel('Player'));
    await client.createSessionRollPrompts(fixture.sessionId, {
      prompts: [{
        targetCharacterId: joined.character.id,
        checkMode: 'Skill',
        skillKey: 'survival',
        resultKind: 'PassFail',
      }],
    });

    await seedAdminAuth(page);
    await page.goto(`/sessions/${fixture.sessionId}/dm`);
    await expect(page.getByText('Awaiting roll')).toBeVisible();
    await page.getByRole('button', { name: 'Cancel' }).first().click();
    await expect(page.getByText('Awaiting roll')).not.toBeVisible({ timeout: 45_000 });
  });
});
