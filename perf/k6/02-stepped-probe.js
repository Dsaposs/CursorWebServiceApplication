import http from 'k6/http';
import { check, sleep } from 'k6';

const fixture = JSON.parse(open('../reports/raw/fixture.json'));
const apiUrl = __ENV.API_URL || fixture.apiUrl || 'http://localhost:5294';
const mode = __ENV.MODE || 'mixed';

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '45s',
  thresholds: {
    http_req_failed: ['rate<0.50'],
  },
};

function playerHeaders(token) {
  return { headers: { 'X-Player-Token': token } };
}

function dmHeaders(token) {
  return { headers: { Authorization: `Bearer ${token}` } };
}

export default function steppedProbe() {
  const player = fixture.players[(__VU - 1) % fixture.players.length];

  if (mode === 'player-poll' || mode === 'mixed') {
    const versionRes = http.get(
      `${apiUrl}/api/session-join/${fixture.joinCode}/version`,
      playerHeaders(player.token),
    );
    check(versionRes, { 'player version 200': response => response.status === 200 });

    if (versionRes.status === 200) {
      const liveRes = http.get(
        `${apiUrl}/api/session-join/${fixture.joinCode}/live`,
        playerHeaders(player.token),
      );
      check(liveRes, { 'player live 200': response => response.status === 200 });
    }
  }

  if (mode === 'dm-poll' || mode === 'mixed') {
    const versionRes = http.get(
      `${apiUrl}/api/sessions/${fixture.sessionId}/version`,
      dmHeaders(fixture.dmToken),
    );
    check(versionRes, { 'dm version 200': response => response.status === 200 });

    if (versionRes.status === 200) {
      const liveRes = http.get(
        `${apiUrl}/api/sessions/${fixture.sessionId}/live`,
        dmHeaders(fixture.dmToken),
      );
      check(liveRes, { 'dm live 200': response => response.status === 200 });
    }
  }

  if (mode === 'action-submit') {
    const actionRes = http.post(
      `${apiUrl}/api/sessions/${fixture.joinCode}/actions`,
      JSON.stringify({
        actionText: `Load action ${__VU}-${__ITER}-${Date.now()}`,
        actionKey: 'observeThreat',
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'X-Player-Token': player.token,
        },
      },
    );
    check(actionRes, { 'action accepted': response => response.status >= 200 && response.status < 300 });
  }

  sleep(Number(__ENV.SLEEP_SEC || 0.4));
}

export function handleSummary(data) {
  const slug = __ENV.OUTPUT_SLUG || `k6-stepped-${mode}-${__ENV.VUS || 0}`;
  return {
    [`../reports/raw/${slug}.json`]: JSON.stringify(data, null, 2),
    stdout: textSummary(data),
  };
}

function textSummary(data) {
  const metrics = data.metrics || {};
  const duration = metrics.http_req_duration?.values || {};
  const failed = metrics.http_req_failed?.values?.rate ?? 0;
  const rps = metrics.http_reqs?.values?.rate ?? 0;
  return [
    `${__ENV.MODE || 'mixed'} @ ${__ENV.VUS || 0} VUs`,
    `  rps=${rps.toFixed(2)} error=${(failed * 100).toFixed(2)}% p95=${(duration['p(95)'] || 0).toFixed(1)}ms`,
  ].join('\n');
}
