import type {
  CampaignResponse,
  CampaignMemberResponse,
  ScheduledSessionResponse,
  CreateCampaignRequest,
  UpdateCampaignRequest,
  AddCampaignMemberRequest,
  CreateScheduledSessionRequest,
  UpdateScheduledSessionRequest,
} from '~/types/api';

export function useCampaigns() {
  const { api } = useApi();

  async function listCampaigns(): Promise<CampaignResponse[]> {
    return api<CampaignResponse[]>('/api/campaigns');
  }

  async function getCampaign(id: string) {
    return api<CampaignResponse & {
      members: CampaignMemberResponse[];
      upcomingSchedule: ScheduledSessionResponse[];
    }>(`/api/campaigns/${id}`);
  }

  async function createCampaign(req: CreateCampaignRequest): Promise<{ id: string }> {
    return api<{ id: string }>('/api/campaigns', { method: 'POST', body: req });
  }

  async function updateCampaign(id: string, req: UpdateCampaignRequest): Promise<void> {
    await api(`/api/campaigns/${id}`, { method: 'PUT', body: req });
  }

  async function deleteCampaign(id: string): Promise<void> {
    await api(`/api/campaigns/${id}`, { method: 'DELETE' });
  }

  async function addMember(campaignId: string, req: AddCampaignMemberRequest): Promise<void> {
    await api(`/api/campaigns/${campaignId}/members`, { method: 'POST', body: req });
  }

  async function removeMember(campaignId: string, memberId: string): Promise<void> {
    await api(`/api/campaigns/${campaignId}/members/${memberId}`, { method: 'DELETE' });
  }

  async function listSchedule(campaignId: string): Promise<ScheduledSessionResponse[]> {
    return api<ScheduledSessionResponse[]>(`/api/campaigns/${campaignId}/schedule`);
  }

  async function addScheduledSession(
    campaignId: string,
    req: CreateScheduledSessionRequest,
  ): Promise<{ id: string }> {
    return api<{ id: string }>(`/api/campaigns/${campaignId}/schedule`, {
      method: 'POST',
      body: req,
    });
  }

  async function updateScheduledSession(
    campaignId: string,
    scheduleId: string,
    req: UpdateScheduledSessionRequest,
  ): Promise<void> {
    await api(`/api/campaigns/${campaignId}/schedule/${scheduleId}`, { method: 'PATCH', body: req });
  }

  async function linkGameSession(
    campaignId: string,
    scheduleId: string,
    sessionId: string,
  ): Promise<void> {
    await api(`/api/campaigns/${campaignId}/schedule/${scheduleId}/link`, {
      method: 'POST',
      body: { sessionId },
    });
  }

  return {
    listCampaigns,
    getCampaign,
    createCampaign,
    updateCampaign,
    deleteCampaign,
    addMember,
    removeMember,
    listSchedule,
    addScheduledSession,
    updateScheduledSession,
    linkGameSession,
  };
}
