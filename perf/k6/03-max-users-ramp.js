import http from 'k6/http';
import { check, sleep } from 'k6';

const fixture = JSON.parse(open('../reports/raw/fixture.json'));
const apiUrl = __ENV.API_URL || fixture.apiUrl || 'http://localhost:5294';

export const options = {
  scenarios: {
    ramp_users: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 10 },
        { duration: '30s', target: 25 },
        { duration: '30s', target: 50 },
        { duration: '30s', target: 75 },
        { duration: '30s', target: 100 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.50'],
  },
};

export default function maxUsersProbe() {
  const playerIndex = (__VU - 1) % fixture.players.length;
  const player = fixture.players[playerIndex];

  const versionRes = http.get(
    `${apiUrl}/api/session-join/${fixture.joinCode}/version`,
    { headers: { 'X-Player-Token': player.token }, tags: { name: 'player_version' } },
  );
  check(versionRes, { 'player version ok': response => response.status === 200 });

  const liveRes = http.get(
    `${apiUrl}/api/session-join/${fixture.joinCode}/live`,
    { headers: { 'X-Player-Token': player.token }, tags: { name: 'player_live' } },
  );
  check(liveRes, { 'player live ok': response => response.status === 200 });

  sleep(0.35);
}

export function handleSummary(data) {
  return {
    '../reports/raw/k6-max-users-ramp.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data),
  };
}

function textSummary(data) {
  const duration = data.metrics?.http_req_duration?.values || {};
  const failed = data.metrics?.http_req_failed?.values?.rate ?? 0;
  const rps = data.metrics?.http_reqs?.values?.rate ?? 0;
  return `max-users ramp: rps=${rps.toFixed(2)} error=${(failed * 100).toFixed(2)}% p95=${(duration['p(95)'] || 0).toFixed(1)}ms`;
}
