/**
 * Thin fetch wrapper with SAFE automatic retries.
 *
 * A request is retried (exponential backoff + full jitter) on network failures and on
 * transient HTTP statuses - but ONLY if repeating it cannot cause a duplicate side effect:
 *   - GET / PUT / DELETE are idempotent by definition;
 *   - POST is retried only when it carries an Idempotency-Key (the server de-duplicates).
 * "Add to cart" (POST without key) is therefore never retried automatically.
 */

export interface RetryPolicy {
  maxAttempts: number;
  baseDelayMs: number;
  maxDelayMs: number;
  retryOnStatuses: readonly number[];
}

export interface HttpClientOptions {
  baseUrl: string;
  retry: RetryPolicy;
  /** Returns the current shopper id for the X-Customer-Id header (null = anonymous). */
  getCustomerId?: () => string | null;
  /** Injectable for tests. */
  fetchImpl?: typeof fetch;
  sleep?: (ms: number) => Promise<void>;
  random?: () => number;
}

export interface RequestOptions {
  query?: Record<string, string | number | boolean | string[] | undefined>;
  body?: unknown;
  idempotencyKey?: string;
}

/** RFC 7807 problem details surfaced as an Error. `code` is the API's stable error code. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly code?: string,
    public readonly errors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface ApiResponse<T> {
  data: T;
  status: number;
  headers: Headers;
}

const IDEMPOTENT_METHODS = new Set(['GET', 'HEAD', 'PUT', 'DELETE']);

export function buildQueryString(query: RequestOptions['query']): string {
  if (!query) return '';
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === '' || (Array.isArray(value) && value.length === 0)) continue;
    if (Array.isArray(value)) value.forEach((v) => params.append(key, v));
    else params.append(key, String(value));
  }
  const text = params.toString();
  return text ? `?${text}` : '';
}

/** Full-jitter backoff: random(0, min(max, base * 2^(attempt-1))). */
export function backoffDelay(attempt: number, policy: RetryPolicy, random: () => number = Math.random): number {
  const exponential = Math.min(policy.maxDelayMs, policy.baseDelayMs * 2 ** (attempt - 1));
  return Math.floor(random() * exponential);
}

export function isRetryable(method: string, hasIdempotencyKey: boolean): boolean {
  return IDEMPOTENT_METHODS.has(method.toUpperCase()) || hasIdempotencyKey;
}

export function createHttpClient(options: HttpClientOptions) {
  const fetchImpl = options.fetchImpl ?? ((input: RequestInfo | URL, init?: RequestInit) => fetch(input, init));
  const sleep = options.sleep ?? ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));
  const random = options.random ?? Math.random;

  async function send<T>(method: string, path: string, request: RequestOptions = {}): Promise<ApiResponse<T>> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    const customerId = options.getCustomerId?.();
    if (customerId) headers['X-Customer-Id'] = customerId;
    if (request.idempotencyKey) headers['Idempotency-Key'] = request.idempotencyKey;
    if (request.body !== undefined) headers['Content-Type'] = 'application/json';

    const url = `${options.baseUrl}${path}${buildQueryString(request.query)}`;
    const canRetry = isRetryable(method, Boolean(request.idempotencyKey));
    const maxAttempts = canRetry ? Math.max(1, options.retry.maxAttempts) : 1;

    for (let attempt = 1; ; attempt++) {
      let response: Response;
      try {
        response = await fetchImpl(url, {
          method,
          headers,
          body: request.body === undefined ? undefined : JSON.stringify(request.body),
        });
      } catch {
        if (attempt < maxAttempts) {
          await sleep(backoffDelay(attempt, options.retry, random));
          continue;
        }
        throw new ApiError(0, 'Network error - please check your connection and try again.', 'network.error');
      }

      if (response.ok) {
        const text = await response.text();
        return { data: (text ? JSON.parse(text) : undefined) as T, status: response.status, headers: response.headers };
      }

      if (attempt < maxAttempts && options.retry.retryOnStatuses.includes(response.status)) {
        const retryAfterSeconds = Number(response.headers.get('Retry-After'));
        const delay = retryAfterSeconds > 0 ? retryAfterSeconds * 1000 : backoffDelay(attempt, options.retry, random);
        await sleep(delay);
        continue;
      }

      throw await toApiError(response);
    }
  }

  return {
    get: <T>(path: string, query?: RequestOptions['query']) => send<T>('GET', path, { query }).then((r) => r.data),
    post: <T>(path: string, body?: unknown, idempotencyKey?: string) => send<T>('POST', path, { body, idempotencyKey }),
    put: <T>(path: string, body?: unknown) => send<T>('PUT', path, { body }).then((r) => r.data),
    delete: <T>(path: string) => send<T>('DELETE', path).then((r) => r.data),
  };
}

export type HttpClient = ReturnType<typeof createHttpClient>;

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const problem = (await response.json()) as {
      title?: string;
      detail?: string;
      code?: string;
      errors?: Record<string, string[]>;
    };
    const firstValidationError = problem.errors ? Object.values(problem.errors).flat()[0] : undefined;
    return new ApiError(
      response.status,
      firstValidationError ?? problem.detail ?? problem.title ?? `Request failed (${response.status})`,
      problem.code,
      problem.errors,
    );
  } catch {
    return new ApiError(response.status, `Request failed (${response.status})`);
  }
}
