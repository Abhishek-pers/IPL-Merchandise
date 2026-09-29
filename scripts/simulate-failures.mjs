// Fault-injection simulation against a RUNNING API: races, lost network packets and a
// dropped database connection. Proves exactly-once effects under at-least-once delivery.
//
//   node scripts/simulate-failures.mjs        (API on http://localhost:5080, frontend deps installed)
//   node scripts/simulate-failures.mjs --scenario=checkout-response-lost (focused retry demo)
//
// How network errors are simulated: a small proxy sits between the client and the API and
//   - "refuse"        -> kills the connection BEFORE the request reaches the API (nothing happened), or
//   - "drop-response" -> lets the API finish (and COMMIT) but kills the connection before the client
//                        sees the response. The client cannot tell which of the two happened, which
//                        is exactly why retries must be idempotent.
// Scenarios 4-6 drive the REAL frontend code (src/api/httpClient.ts + storeApi.ts, compiled on the fly),
// so the automatic-retry behaviour shown is what the browser does.
// Scenario 7 kills the API's PostgreSQL connection in the middle of a checkout transaction (needs psql).
//
// Note: places real orders and consumes demo stock.

import http from 'node:http';
import { spawn, spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const API_ORIGIN = process.env.API_URL ?? 'http://localhost:5080';
const API = `${API_ORIGIN}/api/v1`;
const PROXY_PORT = Number(process.env.PROXY_PORT ?? 5099);
const PROXY = `http://localhost:${PROXY_PORT}/api/v1`;
const PSQL = process.env.PSQL ?? 'C:/Program Files/PostgreSQL/16/bin/psql.exe';
const PG = { PGHOST: 'localhost', PGUSER: 'ipl', PGDATABASE: 'iplstore', PGPASSWORD: process.env.PGPASSWORD ?? 'ipl_local_only' };
const AARAV = '11111111-1111-1111-1111-111111111111';

const results = [];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function check(name, ok, detail) {
  results.push({ name, ok });
  console.log(`  ${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ` - ${detail}` : ''}`);
}

// ------------------------------------------------------------------ raw HTTP helpers

async function call(method, urlPath, { body, key, customer = AARAV, base = API } = {}) {
  const headers = { Accept: 'application/json', 'X-Customer-Id': customer };
  if (key) headers['Idempotency-Key'] = key;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const res = await fetch(`${base}${urlPath}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  return { status: res.status, replayed: res.headers.get('Idempotent-Replayed') === 'true', data: text ? JSON.parse(text) : null };
}

async function emptyCart() {
  const { data } = await call('GET', '/cart');
  for (const line of data.lines) await call('DELETE', `/cart/items/${line.productId}`);
}
const cartLine = async (productId) => (await call('GET', '/cart')).data.lines.find((l) => l.productId === productId);
const stockOf = async (productId) => (await call('GET', `/products/${productId}`)).data.stockQuantity;
const orderCount = async () => (await call('GET', '/orders?pageSize=1')).data.totalCount;

// ------------------------------------------------------------------ fault-injecting proxy

const faults = []; // { method, path, kind: 'refuse' | 'drop-response', seen?: number }
const proxyLog = [];

const proxy = http.createServer((req, res) => {
  const chunks = [];
  req.on('data', (c) => chunks.push(c));
  req.on('end', () => {
    const route = new URL(req.url, 'http://x').pathname;
    const index = faults.findIndex((f) => f.method === req.method && route.startsWith(f.path));
    const fault = index >= 0 ? faults.splice(index, 1)[0] : null;

    if (fault?.kind === 'refuse') {
      proxyLog.push(`${req.method} ${route} -> connection killed before reaching the API`);
      req.socket.destroy();
      return;
    }

    const upstream = http.request(`${API_ORIGIN}${req.url}`, { method: req.method, headers: req.headers }, (up) => {
      const body = [];
      up.on('data', (c) => body.push(c));
      up.on('end', () => {
        if (fault?.kind === 'drop-response') {
          proxyLog.push(`${req.method} ${route} -> API answered ${up.statusCode}, response dropped on the way back`);
          req.socket.destroy();
          return;
        }
        proxyLog.push(`${req.method} ${route} -> ${up.statusCode}`);
        res.writeHead(up.statusCode, up.headers);
        res.end(Buffer.concat(body));
      });
    });
    upstream.on('error', () => req.socket.destroy());
    upstream.end(Buffer.concat(chunks));
  });
});

// ------------------------------------------------------------------ the REAL frontend API client

async function loadFrontendClient() {
  const require = createRequire(path.join(ROOT, 'frontend/package.json'));
  const esbuild = require('esbuild');
  const out = await esbuild.build({
    stdin: {
      contents: `export { createHttpClient } from './src/api/httpClient.ts';
                 export { createStoreApi } from './src/api/storeApi.ts';
                 export { appConfig } from './src/config.ts';`,
      resolveDir: path.join(ROOT, 'frontend'),
      loader: 'ts',
    },
    bundle: true, write: false, format: 'esm', platform: 'node',
    define: { 'import.meta.env': '{}' },
    logLevel: 'silent',
  });
  const code = out.outputFiles[0].text;
  return import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);
}

function browserApi(mod, attempts) {
  const http = mod.createHttpClient({
    baseUrl: PROXY,
    retry: { ...mod.appConfig.retry, baseDelayMs: 50, maxDelayMs: 200 }, // same policy, shorter waits
    getCustomerId: () => AARAV,
    fetchImpl: (url, init) => {
      attempts.push(`${init.method} ${new URL(url).pathname}`);
      return fetch(url, init);
    },
  });
  return mod.createStoreApi(http);
}

// ------------------------------------------------------------------ scenarios

async function pickProduct(sku) {
  const { data } = await call('GET', '/products?pageSize=100');
  const p = data.items.find((i) => i.sku === sku);
  if (!p) throw new Error(`Product ${sku} not found - is the demo catalogue seeded?`);
  return p;
}

async function s1_parallelAdds(product) {
  console.log('\n[1] Race: 5 parallel "add 1 to cart" for the same product and customer');
  await emptyCart();
  const replies = await Promise.all(Array.from({ length: 5 }, () =>
    call('POST', '/cart/items', { body: { productId: product.id, quantity: 1 } })));
  const line = await cartLine(product.id);
  const lines = (await call('GET', '/cart')).data.lines.length;
  console.log(`      statuses ${replies.map((r) => r.status).join(', ')}; cart has ${lines} line(s), quantity ${line?.quantity}`);
  check('no lost update (quantity 5) and no duplicate line', line?.quantity === 5 && lines === 1);
}

async function s2_doubleClickDifferentKeys(product) {
  console.log('\n[2] Race: 5 parallel checkouts of the same cart with DIFFERENT keys (buggy double-click)');
  await emptyCart();
  await call('POST', '/cart/items', { body: { productId: product.id, quantity: 1 } });
  const [stock0, orders0] = [await stockOf(product.id), await orderCount()];
  const replies = await Promise.all(Array.from({ length: 5 }, () =>
    call('POST', '/orders', { key: crypto.randomUUID() })));
  const [stock1, orders1] = [await stockOf(product.id), await orderCount()];
  console.log(`      statuses ${replies.map((r) => `${r.status}${r.data?.code ? ` ${r.data.code}` : ''}`).join(', ')}`);
  check('exactly one order, stock taken once', orders1 - orders0 === 1 && stock0 - stock1 === 1,
    `orders +${orders1 - orders0}, stock ${stock0} -> ${stock1}`);
}

async function s3_sameKeyParallel(product) {
  console.log('\n[3] Race: 5 parallel checkouts with the SAME Idempotency-Key (client retry storm)');
  await emptyCart();
  await call('POST', '/cart/items', { body: { productId: product.id, quantity: 1 } });
  const orders0 = await orderCount();
  const key = crypto.randomUUID();
  const replies = await Promise.all(Array.from({ length: 5 }, () => call('POST', '/orders', { key })));
  const numbers = new Set(replies.map((r) => r.data?.orderNumber));
  console.log(`      statuses ${replies.map((r) => `${r.status}${r.replayed ? ' replayed' : ''}`).join(', ')}`);
  check('one 201, the rest replay the same order', replies.filter((r) => r.status === 201).length === 1
    && numbers.size === 1 && (await orderCount()) - orders0 === 1, [...numbers].join());
}

async function s4_checkoutResponseLost(mod, product) {
  console.log('\n[4] Network: checkout COMMITS but the response is lost -> browser client retries automatically');
  await emptyCart();
  await call('POST', '/cart/items', { body: { productId: product.id, quantity: 1 } });
  const [stock0, orders0] = [await stockOf(product.id), await orderCount()];
  faults.push({ method: 'POST', path: '/api/v1/orders', kind: 'drop-response' });
  const attempts = [];
  proxyLog.length = 0;
  const { order, replayed } = await browserApi(mod, attempts).placeOrder(crypto.randomUUID());
  proxyLog.forEach((l) => console.log(`      proxy: ${l}`));
  const [stock1, orders1] = [await stockOf(product.id), await orderCount()];
  check('user sees success, one order, stock taken once', replayed && orders1 - orders0 === 1 && stock0 - stock1 === 1,
    `${attempts.length} attempts, order ${order.orderNumber} (replayed=${replayed}), stock ${stock0} -> ${stock1}`);
}

async function s5_checkoutRequestLost(mod, product) {
  console.log('\n[5] Network: checkout request never reaches the API -> browser client retries automatically');
  await emptyCart();
  await call('POST', '/cart/items', { body: { productId: product.id, quantity: 1 } });
  const orders0 = await orderCount();
  faults.push({ method: 'POST', path: '/api/v1/orders', kind: 'refuse' });
  const attempts = [];
  proxyLog.length = 0;
  const { order, replayed } = await browserApi(mod, attempts).placeOrder(crypto.randomUUID());
  proxyLog.forEach((l) => console.log(`      proxy: ${l}`));
  check('retry creates the order exactly once', !replayed && (await orderCount()) - orders0 === 1,
    `${attempts.length} attempts, order ${order.orderNumber}`);
}

async function s6_addToCartResponseLost(mod, product) {
  console.log('\n[6] Network: "add 1 to cart" COMMITS but the response is lost; the shopper clicks again');
  await emptyCart();
  faults.push({ method: 'POST', path: '/api/v1/cart/items', kind: 'drop-response' });
  const attempts = [];
  proxyLog.length = 0;
  const api = browserApi(mod, attempts);
  const key = crypto.randomUUID(); // ProductDetailsPage: one key per add intent, reused until it succeeds
  let firstError = null;
  try {
    await api.addToCart(product.id, 1, key);
  } catch (e) {
    firstError = e.message;
  }
  if (firstError) {
    console.log(`      shopper saw: "${firstError}" -> clicks "Add to cart" again`);
    await api.addToCart(product.id, 1, key);
  }
  proxyLog.forEach((l) => console.log(`      proxy: ${l}`));
  const line = await cartLine(product.id);
  check('shopper wanted 1 and has exactly 1 in the cart', line?.quantity === 1,
    `${attempts.length} HTTP attempts${firstError ? '' : ' (client retried by itself)'}, cart quantity ${line?.quantity}`);

  console.log('\n[6b] Race: the same "add" (same key) arrives 5 times in parallel');
  await emptyCart();
  const sameKey = crypto.randomUUID();
  const replies = await Promise.all(Array.from({ length: 5 }, () =>
    call('POST', '/cart/items', { key: sameKey, body: { productId: product.id, quantity: 1 } })));
  const raced = await cartLine(product.id);
  console.log(`      statuses ${replies.map((r) => r.status).join(', ')}`);
  check('applied exactly once', raced?.quantity === 1, `cart quantity ${raced?.quantity}`);
}

async function s7_databaseConnectionKilled(product) {
  console.log('\n[7] Database: the API\'s PostgreSQL connection is killed in the middle of a checkout transaction');
  if (spawnSync(PSQL, ['-tAc', 'select 1'], { env: { ...process.env, ...PG } }).status !== 0) {
    console.log('      skipped - psql not available (set PSQL=path/to/psql)');
    return;
  }
  await emptyCart();
  await call('POST', '/cart/items', { body: { productId: product.id, quantity: 1 } });
  const [stock0, orders0] = [await stockOf(product.id), await orderCount()];

  // Hold the customer's cart lock for 3 s so the checkout transaction is guaranteed to be in flight.
  const locker = spawn(PSQL, ['-q', '-c',
    `BEGIN; SELECT 1 FROM carts WHERE customer_id = '${AARAV}' FOR UPDATE; SELECT pg_sleep(3); COMMIT;`],
  { env: { ...process.env, ...PG, PGAPPNAME: 'sim-locker' } });
  const lockReleased = new Promise((r) => locker.on('exit', r));
  await sleep(500);

  const checkout = call('POST', '/orders', { key: crypto.randomUUID() });
  await sleep(1000);
  const killed = spawnSync(PSQL, ['-tAc',
    `SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity
     WHERE datname = 'iplstore' AND wait_event_type = 'Lock' AND application_name <> 'sim-locker'`],
  { env: { ...process.env, ...PG } }).stdout.toString().trim();
  console.log(`      killed ${killed} API connection(s) while the checkout was waiting inside its transaction`);

  const reply = await checkout;
  await lockReleased;
  const [stock1, orders1] = [await stockOf(product.id), await orderCount()];
  console.log(`      checkout answered HTTP ${reply.status}${reply.data?.orderNumber ? ` order ${reply.data.orderNumber}` : ` ${reply.data?.detail ?? ''}`}`);
  check('server retried the transaction: one order, stock taken once',
    reply.status === 201 && orders1 - orders0 === 1 && stock0 - stock1 === 1,
    `orders +${orders1 - orders0}, stock ${stock0} -> ${stock1}`);
}

// ------------------------------------------------------------------ run

const scenarioNames = [
  'parallel-add',
  'different-key-checkouts',
  'same-key-checkouts',
  'checkout-response-lost',
  'checkout-request-lost',
  'add-response-lost',
  'database-connection-killed',
];
const scenarioArgument = process.argv.find((arg) => arg.startsWith('--scenario='));
const requestedScenario = scenarioArgument?.split('=')[1] ?? 'all';
if (requestedScenario !== 'all' && !scenarioNames.includes(requestedScenario)) {
  console.error(`Unknown scenario '${requestedScenario}'. Choose one of: ${scenarioNames.join(', ')}.`);
  process.exit(2);
}
const selectedScenarios = new Set(requestedScenario === 'all' ? scenarioNames : [requestedScenario]);

await new Promise((r) => proxy.listen(PROXY_PORT, r));
try {
  const needsFrontend = ['checkout-response-lost', 'checkout-request-lost', 'add-response-lost']
    .some((name) => selectedScenarios.has(name));
  const mod = needsFrontend ? await loadFrontendClient() : null;
  const product = await pickProduct(process.env.SKU ?? 'MI-CAP');
  console.log(`Using "${product.name}" (${product.sku}); fault proxy on :${PROXY_PORT} -> ${API_ORIGIN}`);

  if (selectedScenarios.has('parallel-add')) await s1_parallelAdds(product);
  if (selectedScenarios.has('different-key-checkouts')) await s2_doubleClickDifferentKeys(product);
  if (selectedScenarios.has('same-key-checkouts')) await s3_sameKeyParallel(product);
  if (selectedScenarios.has('checkout-response-lost')) await s4_checkoutResponseLost(mod, product);
  if (selectedScenarios.has('checkout-request-lost')) await s5_checkoutRequestLost(mod, product);
  if (selectedScenarios.has('add-response-lost')) await s6_addToCartResponseLost(mod, product);
  if (selectedScenarios.has('database-connection-killed')) await s7_databaseConnectionKilled(product);
  await emptyCart();
} finally {
  proxy.close();
}

const failed = results.filter((r) => !r.ok);
console.log(`\n${results.length - failed.length}/${results.length} passed${failed.length ? ` - FAILED: ${failed.map((f) => f.name).join('; ')}` : ''}`);
process.exit(failed.length ? 1 : 0);
