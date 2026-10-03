environment                    = "dev"
postgres_sku                   = "B_Standard_B1ms"
postgres_high_availability     = false
postgres_read_replica          = false
postgres_backup_retention_days = 7
# Exactly one replica in dev: no cold start during demos, and the in-memory FakePaymentGateway
# (per-replica idempotency) behaves predictably. A real gateway + payments table removes this limit.
api_min_replicas               = 1
api_max_replicas               = 1
