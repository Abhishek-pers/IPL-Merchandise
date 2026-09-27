import type { RetryPolicy } from './api/httpClient';

/**
 * Every client-side tunable in one object. Change a value here, not in the code that uses it.
 */
export const appConfig = {
  /** Relative by default (dev proxy / nginx). Set VITE_API_BASE_URL at build time for a separate API host. */
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
  pageSize: 12,
  retry: {
    maxAttempts: 3,
    baseDelayMs: 300,
    maxDelayMs: 3000,
    retryOnStatuses: [408, 429, 500, 502, 503, 504],
  } satisfies RetryPolicy,
} as const;
