# ADR-0004 · Concurrency strategy

**Context.** Must prevent duplicate carts/lines, lost cart updates, overselling and deadlocks, at low latency.

**Decision.** READ COMMITTED everywhere, plus:
- Cart: `INSERT … ON CONFLICT DO NOTHING` then `SELECT … FOR UPDATE` on the customer's cart row for every cart/checkout transaction.
- Stock: single-statement `UPDATE … SET stock = stock - q WHERE id = @id AND stock >= q`, rows touched in ascending id order.
- Backstops: UNIQUE and CHECK constraints; `xmin` optimistic token on products.
- Transient failures (deadlock, serialization, connection) retried as a whole transaction.

**Consequences.** + Contention only within one customer (carts) or one product row (stock), no user-visible conflicts in normal use. − Hot single-product drops serialise on one row; Phase-4 plan (queue/Redis counter) documented in docs/07.
