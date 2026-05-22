import http from 'k6/http';
import { check, sleep } from 'k6';

const fixture = JSON.parse(open('../reports/raw/fixture.json'));
const apiUrl = __ENV.API_URL || fixture.apiUrl || 'http://localhost:5294';

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '45s',
  thresholds: {
    http_req_failed: ['rate<0.50'],
  },
};

export default function healthBaseline() {
  const health = http.get(`${apiUrl}/health`);
  check(health, {
    'health 200': response => response.status === 200,
  });

  const version = http.get(`${apiUrl}/api/version`);
  check(version, {
    'version 200': response => response.status === 200,
  });

  sleep(0.2);
}

export function handleSummary(data) {
  const slug = __ENV.OUTPUT_SLUG || 'k6-health-baseline';
  return {
    [`../reports/raw/${slug}.json`]: JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const metrics = data.metrics || {};
  const duration = metrics.http_req_duration?.values || {};
  const failed = metrics.http_req_failed?.values?.rate ?? 0;
  const rps = metrics.http_reqs?.values?.rate ?? 0;
  const lines = [
    `health baseline (${options.vus || __ENV.VUS} VUs)`,
    `  rps=${rps.toFixed(2)} error=${(failed * 100).toFixed(2)}% p95=${(duration['p(95)'] || 0).toFixed(1)}ms`,
  ];
  return lines.join('\n');
}
