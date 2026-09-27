# ADR-0003 · Denormalised catalogue maintained by triggers

**Context.** Catalogue browsing/search dominates traffic and needs product + franchise + category per row, filterable on each.

**Decision.** `product_catalog` flat table, refreshed by `AFTER` triggers on `products`, `franchises`, `product_categories` via an idempotent `project_product()` upsert. Served through `ReadOnlyStoreDbContext` (replica-capable).

**Alternatives.** Views (join every time), materialised views (stale until refresh), app-maintained projection (every writer must remember), CDC → search engine (more infra; the Phase-3 option).

**Consequences.** + Same-transaction consistency, no joins on the hot path, writer-agnostic. − Logic lives in the DB (documented here and in V002; covered by an integration test); bulk franchise renames update many rows (acceptable: rare).
