environment                    = "dev"
postgres_sku                   = "B_Standard_B1ms"
postgres_high_availability     = false
postgres_read_replica          = false
postgres_backup_retention_days = 7
api_min_replicas               = 0 # scale to zero when idle
api_max_replicas               = 2
