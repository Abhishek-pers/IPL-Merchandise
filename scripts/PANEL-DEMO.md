# Developer Panel Demos

These are developer-only terminal scripts. They add no monitoring pages, admin routes, or controls to the shopper web app.

From the workspace PowerShell prompt at `C:\Users\ASUS\Downloads\tt`, first enter the project folder:

```powershell
Set-Location '.\ParkPlace Assigment'
```

Then run the commands below. The API must be running at `http://localhost:5080`, and PostgreSQL must be the local/demo database. These scripts write to that database; do not run them against production.

## 1. API Request -> PostgreSQL Change

```powershell
node scripts/demo-api-database.mjs
```

The script sends `POST /api/v1/cart/items` as the seeded Aarav customer, reports the cart quantity before and after, and prints SQL to check the `cart_items` and `idempotency_keys` rows in `psql`. Each run adds one unit to Aarav's MI-CAP cart.

For the matching browser request, open Developer Tools > Network, add a product to the cart in the shopper UI, and inspect `POST /api/v1/cart/items`. Then run the printed SQL in the `psql` terminal.

## 2. Parallel Checkouts, Same Idempotency Key

```powershell
node scripts/demo-concurrency.mjs --idempotency-only
```

Five checkout requests run in parallel with one key. Expected: one `201 Created`, four `200` replays, one distinct order, one additional saved order, and one unit of stock consumed. The script prints SQL to verify the order and stock.

This creates a real order, decrements stock, and clears Aarav's cart before starting.

## 3. Retry After a Lost Network Response

```powershell
node scripts/simulate-failures.mjs --scenario=checkout-response-lost
```

The script starts a local fault proxy on port `5099`. The proxy lets checkout commit, then drops the response. The real frontend HTTP client retries with the same idempotency key. Expected: the retry receives the existing order; order count and stock show that only one checkout took effect.

This creates a real order and consumes one unit of stock. It requires frontend dependencies to be installed so the script can bundle the actual HTTP client using esbuild.

## SQL to Show in `psql`

At the `iplstore=>` prompt, paste the exact query printed by a script, or use these general checks:

```sql
\dt
SELECT sku, name, stock_quantity FROM products WHERE sku = 'MI-CAP';
SELECT order_number, status, idempotency_key FROM orders ORDER BY placed_at DESC LIMIT 5;
SELECT customer_id, operation, idempotency_key FROM idempotency_keys ORDER BY created_at DESC LIMIT 5;
```