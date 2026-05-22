import { test, expect } from '@playwright/test';
import { ApiClient, createSessionFixture, loginAdmin } from '../helpers/api-client';
import { E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD, uniqueLabel } from '../helpers/test-data';
import {
  dmExpandPendingActions,
  dmPromptActorRoll,
  dmPublishResolution,
  joinSessionAsNewCharacter,
  playerSubmitAutoRoll,
  seedAdminAuth,
  submitExplorationAction,
} from '../helpers/ui-flows';

const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5294';

test.describe('Exploration action workflow', () => {
  test('player submits an action, rolls when prompted, and DM publishes the resolution', async ({ browser }) => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);

    const characterName = uniqueLabel('Observer');
    const playerName = uniqueLabel('Player');
    const resolutionNote = uniqueLabel('You spot movement in the vents');

    const playerContext = await browser.newContext();
    const dmContext = await browser.newContext();
    const playerPage = await playerContext.newPage();
    const dmPage = await dmContext.newPage();

    await joinSessionAsNewCharacter(playerPage, fixture.joinCode, characterName, playerName);
    await submitExplorationAction(playerPage, 'Observe Threat');

    await seedAdminAuth(dmPage);
    await dmPage.goto(`/sessions/${fixture.sessionId}/dm`);
    await dmExpandPendingActions(dmPage);
    await dmPromptActorRoll(dmPage);

    await playerSubmitAutoRoll(playerPage);

    await dmPublishResolution(dmPage, resolutionNote);

    await expect(dmPage.getByRole('button', { name: /Exploration Actions outside of combat/i })).toBeVisible();
    await playerPage.getByRole('button', { name: new RegExp(`${characterName}.*Observe Threat`) }).click();
    await expect(playerPage.getByText(resolutionNote)).toBeVisible({ timeout: 45_000 });

    await playerContext.close();
    await dmContext.close();
  });

  test('API action submit and resolve updates action status', async () => {
    const token = await loginAdmin(apiUrl, E2E_ADMIN_EMAIL, E2E_ADMIN_PASSWORD);
    const fixture = await createSessionFixture(apiUrl, token);
    const dmClient = new ApiClient({ apiUrl, token });

    const joined = await dmClient.joinSession(fixture.joinCode, uniqueLabel('Operator'), uniqueLabel('Player'));
    const playerClient = new ApiClient({ apiUrl, playerToken: joined.participantToken });

    const submitted = await playerClient.submitPlayerAction(fixture.joinCode, 'Scan the motion tracker');
    expect(submitted.status).toMatch(/Pending|DmReviewing/i);

    const resolved = await dmClient.resolveAction(submitted.id, 'The tracker pings something moving.');
    expect(resolved.status).toBe('Published');
  });
});
