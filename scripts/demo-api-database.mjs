// Demonstrate an API write and inspect its persisted PostgreSQL rows.
// Run from the repository root while the API and local PostgreSQL are running:
//   node scripts/demo-api-database.mjs
// Optional: API_URL=http://localhost:5080 SKU=MI-CAP node scripts/demo-api-database.mjs

const API = `${process.env.API_URL ?? 'http://localhost:5080'}/api/v1`;
const CUSTOMER_ID = '11111111-1111-1111-1111-111111111111';
const SKU = process.env.SKU ?? 'MI-CAP';

async function call(method, path, { body, idempotencyKey, customerId } = {}) {
  const headers = { Accept: 'application/json' };
  if (customerId) headers['X-Customer-Id'] = customerId;
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const response = await fetch(`${API}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${data?.detail ?? text}`);
  return { status: response.status, data };
}

const productResult = await call('GET', `/products?search=${encodeURIComponent(SKU)}&pageSize=100`);
const product = productResult.data.items.find((item) => item.sku === SKU);
if (!product) throw new Error(`Could not find ${SKU}; check that the demo catalogue is seeded.`);

const beforeResult = await call('GET', '/cart', { customerId: CUSTOMER_ID });
const previousLine = beforeResult.data.lines.find((line) => line.productId === product.id);
const quantityBefore = previousLine?.quantity ?? 0;
const idempotencyKey = crypto.randomUUID();

console.log('=== API request -> PostgreSQL cart write ===');
console.log(`Customer: ${CUSTOMER_ID}`);
console.log(`Product: ${product.sku} (${product.id})`);
console.log(`Cart quantity before: ${quantityBefore}`);
console.log(`POST ${API}/cart/items`);
console.log(`Idempotency-Key: ${idempotencyKey}`);

const result = await call('POST', '/cart/items', {
  customerId: CUSTOMER_ID,
  idempotencyKey,
  body: { productId: product.id, quantity: 1 },
});
const updatedLine = result.data.lines.find((line) => line.productId === product.id);
const quantityAfter = updatedLine?.quantity ?? 0;

console.log(`HTTP ${result.status}`);
console.log(`Cart quantity after: ${quantityAfter}`);
console.log(`Verification: ${quantityAfter === quantityBefore + 1 ? 'PASS' : 'FAIL'} (expected exactly one added unit)`);
console.log('\nPaste these into the psql terminal to see the persisted rows:');
console.log(`SELECT customer_id, product_id, quantity FROM cart_items WHERE customer_id = '${CUSTOMER_ID}' AND product_id = '${product.id}';`);
console.log(`SELECT operation, idempotency_key, created_at FROM idempotency_keys WHERE customer_id = '${CUSTOMER_ID}' AND idempotency_key = '${idempotencyKey}';`);

if (quantityAfter !== quantityBefore + 1) process.exit(1);