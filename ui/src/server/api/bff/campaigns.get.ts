import type { CampaignResponse, ScheduledSessionResponse } from '~/types/api';

/**
 * BFF aggregation: fetches campaigns and each campaign's upcoming schedule,
 * then attaches the next scheduled session time to each card.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const base = config.apiBaseUrl as string;
  const authHeader = getHeader(event, 'authorization') ?? '';

  const campaigns = await $fetch<CampaignResponse[]>(`${base}/api/campaigns`, {
    headers: { authorization: authHeader },
  });

  const enriched = await Promise.all(
    campaigns.map(async (c) => {
      try {
        const schedule = await $fetch<ScheduledSessionResponse[]>(
          `${base}/api/campaigns/${c.id}/schedule`,
          { headers: { authorization: authHeader } },
        );
        const next = schedule
          .filter((s) => !s.isCancelled && new Date(s.scheduledAt) >= new Date())
          .sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime())[0];
        return { ...c, nextSession: next ?? null };
      } catch {
        return { ...c, nextSession: null };
      }
    }),
  );

  return enriched;
});
