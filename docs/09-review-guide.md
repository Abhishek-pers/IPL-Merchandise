# 09 · Review guide (for the panel discussion)

## Suggested 20-minute walkthrough order

1. **README** covers the requirements map and the guarantees table (2 min).
2. **docs/01**: layer diagram. Then open `backend/src` and show the project references (3 min).
3. **Domain:** `Cart.cs` has rules with no infrastructure. `Order.Place` is a factory with invariants (3 min).
4. **Checkout:** `CheckoutService.PlaceOrderInTransactionAsync`, then `CartRepository` (lock) and `ProductRepository` (atomic decrement) (5 min).
5. **Database:** `V001` constraints, `V002` read model + triggers, ER diagram in docs/03 (3 min).
6. **Tests:** run `ConcurrencyTests`. Show that the oversell test fires 8 parallel checkouts and exactly 3 succeed (2 min).
7. **Config:** change `Pricing:TaxRate` live and refresh the cart (1 min).
8. **Delivery:** docs/07 diagrams, `compute.tf` `ignore_changes`, `deploy-environment.yml` blue/green (1 min).

## Trade-offs I'd defend

| Decision | Alternative | Why this one |
|---|---|---|
| SQL-first migrations + EF as mapper | EF Code-First migrations | Triggers, generated columns and partial/trigram indexes are first-class. DBA-reviewable. Schema isn't tied to the ORM |
| Pessimistic lock on the cart row | Optimistic concurrency / SERIALIZABLE | Contention is per customer only, and there are no user-visible 409s. See docs/04 §4.2 |
| Conditional `UPDATE` for stock | Reservation table with TTL | Simplest correct thing. A reservation table is the phase 4 answer for flash sales |
| Trigger-maintained read model | App-maintained or CDC projection | Same-transaction consistency with zero app code. Downside: logic in the DB (ADR-0003) |
| No MediatR | MediatR pipeline behaviours | Fewer moving parts and no licensing concerns. `IUnitOfWork` gives the same "wrap the use case" effect |
| Header-based identity | Real auth | Out of scope for the brief. Isolated behind `ICurrentCustomerAccessor` |
| Container Apps | AKS / App Service | Revisions for blue/green, KEDA scaling, jobs for migrations, no cluster to operate |

## Questions to expect (with short answers)

- **How do you guarantee no overselling at 10k RPS?** One conditional `UPDATE` holds a row lock only until commit. The `CHECK` constraint is the backstop. For extreme hot-item contention: a Redis counter or a queue in front of checkout (roadmap phase 4).
- **What if the client times out after the order committed?** It retries with the same `Idempotency-Key` and gets `200` with the same order.
- **What if the DB fails over mid-checkout?** Npgsql raises a transient error. `EfUnitOfWork` re-runs the whole transaction on the promoted standby, and idempotency covers commit ambiguity.
- **How do you add caching?** Catalogue reads are behind `ICatalogQueries`. Add a caching decorator or output cache on `GET /products`, and invalidate on product change (outbox event).
- **Is `DbContext` thread-safe?** No, which is why it is scoped per request and never shared, and why all EF calls are awaited sequentially.
- **Why is `product_id` on order_items not a foreign key?** Order history must survive catalogue deletion. The snapshot columns carry the data.
- **How would you test the retry logic?** Unit tests for the client (`httpClient.test.ts`). For the server, inject a failing `DbConnection` interceptor or kill the connection in an integration test (`pg_terminate_backend`) and assert that exactly one order exists.
- **Why UUID PKs?** No central sequence, shard-friendly, safe to generate in the app. Their cost is index size and random inserts, which is acceptable at this scale. UUIDv7 (time-ordered, `Guid.CreateVersion7()` in .NET 9) is a drop-in improvement.
