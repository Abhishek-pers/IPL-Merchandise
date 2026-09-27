# ADR-0006 · Azure Container Apps, separate infra and app pipelines

**Context.** Need managed hosting with autoscaling, zero-downtime deploys and a place to run one-off migration jobs; team is small.

**Decision.** Container Apps (API, `Multiple` revision mode for blue/green) + Container Apps Job (migrations) + PostgreSQL Flexible Server + Static Web Apps + Key Vault via managed identity. Terraform owns infrastructure shape; the app pipeline owns image versions (`ignore_changes` on image). GitHub Actions with OIDC.

**Consequences.** + No cluster ops, scale-to-zero in dev, instant rollback by traffic shift, no stored cloud credentials. − Less control than AKS (sidecars, custom networking) — revisit if the platform grows to many services.
