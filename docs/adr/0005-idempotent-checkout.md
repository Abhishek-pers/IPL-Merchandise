# ADR-0005 · Idempotency keys for checkout

**Context.** Networks fail after the server commits; clients (and users) retry. Retrying a non-idempotent POST creates duplicate orders.

**Decision.** `POST /orders` requires an `Idempotency-Key` header; `(customer_id, idempotency_key)` is UNIQUE; the lookup happens after acquiring the cart lock. Repeats return 200 + the original order + `Idempotent-Replayed: true`. The SPA keeps the key until success.

**Consequences.** + Exactly-once order creation under retries, double-clicks and commit ambiguity; makes server-side transaction retry safe. − Clients must generate keys (one line: `crypto.randomUUID()`); keys are retained with orders (small).
