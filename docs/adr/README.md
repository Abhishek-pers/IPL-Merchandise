# Architecture Decision Records

One decision per file: context → decision → consequences. New decisions get the next number; superseded ones are marked, never deleted.

| # | Decision | Status |
|---|---|---|
| [0001](0001-clean-architecture.md) | Clean Architecture with ports & adapters | Accepted |
| [0002](0002-postgresql-sql-first-migrations.md) | PostgreSQL + SQL-first migrations, EF Core as mapper | Accepted |
| [0003](0003-trigger-maintained-read-model.md) | Denormalised catalogue read model maintained by triggers | Accepted |
| [0004](0004-concurrency-strategy.md) | Row lock per cart + atomic conditional stock update | Accepted |
| [0005](0005-idempotent-checkout.md) | Idempotency keys for checkout | Accepted |
| [0006](0006-azure-container-apps.md) | Azure Container Apps + separate infra/app pipelines | Accepted |
