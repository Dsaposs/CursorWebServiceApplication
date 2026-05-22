export interface NumericSummary {
  min: number;
  max: number;
  avg: number;
  p50: number;
  p95: number;
  p99: number;
  count: number;
}

export function percentile(values: number[], p: number): number {
  if (!values.length) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  const index = Math.min(sorted.length - 1, Math.ceil((p / 100) * sorted.length) - 1);
  return sorted[index];
}

export function summarize(values: number[]): NumericSummary {
  if (!values.length) {
    return { min: 0, max: 0, avg: 0, p50: 0, p95: 0, p99: 0, count: 0 };
  }

  const total = values.reduce((sum, value) => sum + value, 0);
  return {
    min: Math.min(...values),
    max: Math.max(...values),
    avg: total / values.length,
    p50: percentile(values, 50),
    p95: percentile(values, 95),
    p99: percentile(values, 99),
    count: values.length,
  };
}

export function round(value: number, digits = 1) {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
}

export function formatMs(value: number) {
  return `${round(value)} ms`;
}

export function formatRate(value: number) {
  return `${round(value, 2)}/s`;
}

export function formatPercent(value: number) {
  return `${round(value * 100, 2)}%`;
}

export function formatBytes(bytes: number) {
  if (bytes >= 1024 ** 3) return `${round(bytes / 1024 ** 3, 2)} GiB`;
  if (bytes >= 1024 ** 2) return `${round(bytes / 1024 ** 2, 1)} MiB`;
  if (bytes >= 1024) return `${round(bytes / 1024, 0)} KiB`;
  return `${round(bytes, 0)} B`;
}

export interface K6MetricValue {
  avg?: number;
  min?: number;
  max?: number;
  med?: number;
  'p(90)'?: number;
  'p(95)'?: number;
  'p(99)'?: number;
  count?: number;
  rate?: number;
}

export interface K6SummaryFile {
  metrics?: Record<string, { values?: K6MetricValue }>;
  root_group?: {
    checks?: { passes?: number; fails?: number };
  };
}

export function readK6Summary(summary: K6SummaryFile) {
  const duration = summary.metrics?.http_req_duration?.values;
  const failed = summary.metrics?.http_req_failed?.values?.rate ?? 0;
  const reqs = summary.metrics?.http_reqs?.values?.rate ?? 0;
  const checksPass = summary.root_group?.checks?.passes ?? 0;
  const checksFail = summary.root_group?.checks?.fails ?? 0;

  return {
    rps: reqs,
    errorRate: failed,
    p50Ms: duration?.med ?? duration?.['p(90)'] ?? 0,
    p95Ms: duration?.['p(95)'] ?? 0,
    p99Ms: duration?.['p(99)'] ?? 0,
    avgMs: duration?.avg ?? 0,
    maxMs: duration?.max ?? 0,
    checksPass,
    checksFail,
  };
}

export function findDegradationPoint<T extends { errorRate: number; p95Ms: number }>(
  rows: T[],
  errorThreshold: number,
  p95Threshold: number,
) {
  return rows.find(row => row.errorRate >= errorThreshold || row.p95Ms >= p95Threshold) ?? null;
}

export function findFailurePoint<T extends { errorRate: number }>(
  rows: T[],
  failThreshold: number,
) {
  return rows.find(row => row.errorRate >= failThreshold) ?? null;
}
