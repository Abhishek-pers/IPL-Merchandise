# 05 · API reference (v1)

Base URL: `http://localhost:5080/api/v1`. Interactive docs: **`/swagger`**. Ready-made requests: `backend/src/IplStore.Api/IplStore.Api.http`.

## Headers

| Header | Where | Meaning |
|---|---|---|
| `X-Customer-Id: <uuid>` | cart and orders endpoints | Caller identity (demo stand-in for a JWT). Missing or invalid returns `401` |
| `Idempotency-Key: <string ≤100>` | `POST /orders` (required), `POST /cart/items` (optional, the SPA always sends it) | Makes checkout and add-to-cart safe to retry. See docs/04 |
| `Idempotent-Replayed: true` | response of a replayed checkout | The original order is being returned |

## Endpoints

| Method | Path | Body / query | Success | Errors |
|---|---|---|---|---|
| GET | `/products` | `search, franchise[], category[], inStockOnly, sort (Name, PriceLowToHigh, PriceHighToLow, Newest), page, pageSize` | 200 `PagedResult<ProductSummary>` | 400 |
| GET | `/products/{id}` | – | 200 `ProductDetails` | 404 `product.not_found` |
| GET | `/franchises` | – | 200 `Franchise[]` (cached 5 min) | – |
| GET | `/categories` | – | 200 `Category[]` (cached 5 min) | – |
| GET | `/customers` | – | 200 demo customers | – |
| GET | `/cart` | – | 200 `Cart` (empty cart if none) | 401 |
| POST | `/cart/items` | `{ productId, quantity }` (adds and merges) + optional `Idempotency-Key` (a repeat adds nothing) | 200 `Cart` | 404, 422 `cart.quantity_limit_exceeded`, `cart.line_limit_reached`, `product.insufficient_stock` |
| PUT | `/cart/items/{productId}` | `{ quantity }` (absolute, 0 removes) | 200 `Cart` | 422 `cart.item_not_found` |
| DELETE | `/cart/items/{productId}` | – | 200 `Cart` | 422 `cart.item_not_found` |
| POST | `/orders` | – (uses the cart) + `Idempotency-Key` | 201 `OrderDetails` / 200 replay | 400, 422 `cart.empty`, `product.insufficient_stock`, `product.unavailable`, 409 |
| GET | `/orders` | `page, pageSize` | 200 `PagedResult<OrderSummary>` newest first | 401 |
| GET | `/orders/{id}` | – | 200 `OrderDetails` | 404 (also for another customer's order, so ids can't be probed) |
| GET | `/health/live`, `/health/ready` | – | 200 / 503 | – |

## Example: problem response

```json
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json

{
  "title": "Business rule violated",
  "status": 422,
  "detail": "Not enough stock for 'Delhi Capitals Captain's Autographed Photo (A4, framed)'. Please reduce the quantity.",
  "code": "product.insufficient_stock",
  "traceId": "00-6b1f…-01"
}
```

## Example: cart

```json
{
  "cartId": "5c0d…",
  "lines": [
    { "productId": "…", "sku": "MI-CAP", "productName": "Mumbai Indians Team Cap", "franchiseCode": "MI",
      "franchiseName": "Mumbai Indians", "categoryName": "Cap", "unitPrice": 799.00, "quantity": 2,
      "lineTotal": 1598.00, "isAvailable": true, "availableStock": 199 }
  ],
  "totalQuantity": 2,
  "price": { "subtotal": 1598.00, "tax": 287.64, "shipping": 0.00, "total": 1885.64, "currency": "INR" },
  "updatedAt": "2026-09-24T10:15:02Z"
}
```

## Versioning and compatibility

- Routes are prefixed with `/api/v1`. Breaking changes go to `/api/v2` while v1 keeps running.
- Adding response fields is non-breaking. Clients must ignore unknown fields.
- Error `code` values are a public contract (`DomainErrorCodes.cs`). Codes are only ever added, never renamed.
