# ADR-0001 · Clean Architecture with ports & adapters

**Context.** The panel will change requirements live; the code must absorb change locally and stay testable without a database.

**Decision.** Four projects: Domain (rules, no deps) ← Application (use cases + interfaces) ← Infrastructure (EF/Npgsql adapters) and Api (HTTP + composition root). Tunables are options objects; inputs are parameter objects.

**Consequences.** + Business rules unit-tested in milliseconds with fakes; technology swaps are local to Infrastructure; project references enforce the rules.
− More files/interfaces than a single-project app; mitigated by feature folders (Catalog, Carts, Orders) and the change playbook (docs/08).
