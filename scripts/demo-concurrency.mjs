// Live proof of the concurrency guarantees against a RUNNING API (no Docker needed).
//
//   node scripts/demo-concurrency.mjs            (API must be up on http://localhost:5080)
//   API_URL=http://host:port node scripts/demo-concurrency.mjs
//
// Demo 1 - exactly-once checkout: 5 parallel POST /orders with the SAME Idempotency-Key
//          -> exactly one 201 Created, the rest 200 with Idempotent-Replayed: true, one order number.
// Demo 2 - no overselling: two shoppers each hold more than half of the remaining stock of one
//          product and check out at the same instant -> exactly one succeeds, the other gets 422.
//
// Note: both demos place real orders and consume demo stock.

const API = `${process.env.API_URL ?? 'http://localhost:5080'}/api/v1`;
const AARAV = '11111111-1111-1111-1111-111111111111';
const PRIYA = '22222222-2222-2222-2222-222222222222';

async function call(method, path, customerId, { body, idempotencyKey } = {}) {
  const headers = { Accept: 'application/json' };
  if (customerId) headers['X-Customer-Id'] = customerId;
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const res = await fetch(`${API}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  return { status: res.status, replayed: res.headers.get('Idempotent-Replayed') === 'true', data: text ? JSON.parse(text) : null };
}

async function emptyCart(customerId) {
  const { data: cart } = await call('GET', '/cart', customerId);
  for (const line of cart.lines) await call('DELETE', `/cart/items/${line.productId}`, customerId);
}

async function addToCart(customerId, productId, quantity) {
  const res = await call('POST', '/cart/items', customerId, { body: { productId, quantity } });
  if (res.status !== 200) throw new Error(`Add to cart failed (${res.status}): ${res.data?.detail}`);
}

async function findProduct(predicate) {
  const all = [];
  for (let page = 1; ; page++) {
    const res = await call('GET', `/products?page=${page}&pageSize=100&inStockOnly=true`, null);
    all.push(...res.data.items);
    if (!res.data.hasNextPage) break;
  }
  for (const summary of all) {
    const { data: details } = await call('GET', `/products/${summary.id}`, null);
    if (predicate(details)) return details;
  }
  return null;
}

async function demoIdempotency() {
  console.log('\n=== Demo 1: exactly-once checkout (same Idempotency-Key sent 5x in parallel) ===');
  await emptyCart(AARAV);
  const product = await findProduct((p) => p.stockQuantity >= 1);
  await addToCart(AARAV, product.id, 1);

  const key = crypto.randomUUID();
  const results = await Promise.all(
    Array.from({ length: 5 }, () => call('POST', '/orders', AARAV, { idempotencyKey: key })));

  results.forEach((r, i) =>
    console.log(`  request ${i + 1}: HTTP ${r.status}${r.replayed ? ' (replayed)' : ''}  order ${r.data?.orderNumber}`));
  const created = results.filter((r) => r.status === 201).length;
  const orderNumbers = new Set(results.map((r) => r.data?.orderNumber));
  console.log(`  -> ${created} created, ${results.length - created} replayed, ${orderNumbers.size} distinct order(s)`);
  return created === 1 && orderNumbers.size === 1;
}

async function demoNoOversell() {
  console.log('\n=== Demo 2: no overselling (two shoppers race for the same limited stock) ===');
  await emptyCart(AARAV);
  await emptyCart(PRIYA);

  // Each shopper takes MORE than half of the stock, so both orders cannot fit.
  // Cart:MaxQuantityPerLine is 10 by default, so we need a product with stock <= 18.
  const product = await findProduct((p) => p.stockQuantity >= 1 && p.stockQuantity <= 18);
  if (!product) throw new Error('No product with stock between 1 and 18 left - reseed the database.');
  const quantity = Math.floor(product.stockQuantity / 2) + 1;
  console.log(`  "${product.name}": stock ${product.stockQuantity}, each shopper checks out ${quantity}`);

  await addToCart(AARAV, product.id, quantity);
  await addToCart(PRIYA, product.id, quantity);

  const [a, p] = await Promise.all([
    call('POST', '/orders', AARAV, { idempotencyKey: crypto.randomUUID() }),
    call('POST', '/orders', PRIYA, { idempotencyKey: crypto.randomUUID() }),
  ]);
  for (const [who, r] of [['Aarav', a], ['Priya', p]]) {
    console.log(`  ${who}: HTTP ${r.status}  ${r.status === 201 ? `order ${r.data.orderNumber}` : `${r.data?.code}: ${r.data?.detail}`}`);
  }

  const { data: after } = await call('GET', `/products/${product.id}`, null);
  console.log(`  stock after: ${after.stockQuantity} (expected ${product.stockQuantity - quantity}, never negative)`);

  // Leave the loser's cart clean for the UI demo.
  await emptyCart(AARAV);
  await emptyCart(PRIYA);
  const successes = [a, p].filter((r) => r.status === 201).length;
  return successes === 1 && after.stockQuantity === product.stockQuantity - quantity;
}

const ok1 = await demoIdempotency();
const ok2 = await demoNoOversell();
console.log(`\nResult: idempotency ${ok1 ? 'PASS' : 'FAIL'}, no-oversell ${ok2 ? 'PASS' : 'FAIL'}`);
process.exit(ok1 && ok2 ? 0 : 1);
