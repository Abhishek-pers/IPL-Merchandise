# database/

| Folder | Purpose | Rules |
|---|---|---|
| `migrations/` | Schema + reference data. `V###__description.sql`, applied once, in order | **Never edit an applied script** (checksum check fails start-up). Add `V004__...sql` instead. Keep LF line endings |
| `seed/` | Demo catalogue + demo customers | Must be idempotent (`ON CONFLICT DO NOTHING`); runs on every start when `Database:SeedDemoData=true` |

Scripts are embedded into `IplStore.Infrastructure.dll` and applied by `SqlScriptDatabaseMigrator`
(API start-up locally, `--migrate-only` job in the cloud). Applied versions are recorded in `schema_migrations`.

Run manually against any PostgreSQL 16:
```bash
for f in migrations/*.sql seed/*.sql; do psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$f"; done
```
See `docs/03-database-er.md` for the ER model and the denormalisation rationale.
