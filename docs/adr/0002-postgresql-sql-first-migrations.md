# ADR-0002 · PostgreSQL with SQL-first migrations

**Context.** Needs ACID, strong concurrency primitives, and a path to horizontal scale. Schema uses features ORMs model poorly (triggers, generated columns, partial & trigram indexes, CHECKs).

**Decision.** PostgreSQL 16. Versioned `.sql` files in `/database` are the schema source of truth, embedded in the API and applied by a small migrator (ordered, transactional, checksummed, advisory-locked). EF Core 8 maps to the tables (snake_case convention) but never generates DDL.

**Consequences.** + Reviewable DDL, full PostgreSQL power, ORM-independent schema, safe multi-instance start-up, `--migrate-only` job for CD.
− Model/schema drift is caught by integration tests rather than the compiler; developers write SQL for schema changes.
