import { describe, expect, it, vi } from 'vitest';
import { ApiError, backoffDelay, buildQueryString, createHttpClient, type RetryPolicy } from './httpClient';

const policy: RetryPolicy = { maxAttempts: 3, baseDelayMs: 100, maxDelayMs: 1000, retryOnStatuses: [503] };

function json(status: number, body: unknown, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } });
}

function client(fetchImpl: typeof fetch) {
  return createHttpClient({ baseUrl: '/api/v1', retry: policy, fetchImpl, sleep: async () => {}, getCustomerId: () => 'cust-1' });
}

describe('httpClient', () => {
  it('retries an idempotent GET on a transient status and then succeeds', async () => {
    const fetchImpl = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(json(503, {}))
      .mockResolvedValueOnce(json(200, { ok: true }));

    await expect(client(fetchImpl).get('/cart')).resolves.toEqual({ ok: true });
    expect(fetchImpl).toHaveBeenCalledTimes(2);
  });

  it('never retries a POST without an idempotency key (would duplicate the side effect)', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(json(503, { detail: 'down' }));

    await expect(client(fetchImpl).post('/cart/items', { productId: 'p', quantity: 1 })).rejects.toBeInstanceOf(ApiError);
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it('retries a POST that carries an idempotency key, re-sending the same key', async () => {
    const fetchImpl = vi.fn<typeof fetch>()
      .mockRejectedValueOnce(new TypeError('network down'))
      .mockResolvedValueOnce(json(201, { id: 'order-1' }));

    const result = await client(fetchImpl).post('/orders', undefined, 'key-123');

    expect(result.data).toEqual({ id: 'order-1' });
    const keys = fetchImpl.mock.calls.map(([, init]) => (init?.headers as Record<string, string>)['Idempotency-Key']);
    expect(keys).toEqual(['key-123', 'key-123']);
  });

  it('does not retry business errors and exposes the API error code', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(
      json(422, { title: 'Business rule violated', detail: 'Not enough stock', code: 'product.insufficient_stock' }),
    );

    const error = await client(fetchImpl).get('/cart').catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 422, code: 'product.insufficient_stock', message: 'Not enough stock' });
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it('gives up after maxAttempts', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(json(503, {}));

    await expect(client(fetchImpl).get('/cart')).rejects.toMatchObject({ status: 503 });
    expect(fetchImpl).toHaveBeenCalledTimes(policy.maxAttempts);
  });

  it('sends the customer header', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(json(200, {}));

    await client(fetchImpl).get('/cart');

    expect((fetchImpl.mock.calls[0][1]?.headers as Record<string, string>)['X-Customer-Id']).toBe('cust-1');
  });
});

describe('helpers', () => {
  it('builds query strings, skipping empty values and repeating arrays', () => {
    expect(buildQueryString({ search: 'cap', franchise: ['CSK', 'MI'], sort: undefined, category: [] })).toBe(
      '?search=cap&franchise=CSK&franchise=MI',
    );
    expect(buildQueryString({})).toBe('');
  });

  it('caps exponential backoff at maxDelayMs', () => {
    expect(backoffDelay(1, policy, () => 0.999)).toBe(99);
    expect(backoffDelay(10, policy, () => 0.999)).toBe(999);
  });
});
