# Every environment difference is a variable; the code is identical for dev and prod.

variable "environment" {
  description = "Environment name: dev | prod."
  type        = string
  validation {
    condition     = contains(["dev", "prod"], var.environment)
    error_message = "environment must be dev or prod."
  }
}

variable "location" {
  description = "Azure region (Central India keeps latency low for Indian fans)."
  type        = string
  default     = "centralindia"
}

variable "project" {
  description = "Short name used in resource names."
  type        = string
  default     = "iplstore"
}

variable "api_image" {
  description = "Initial API image. CD pipeline replaces it on every release (Terraform ignores later image changes)."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

# ---------------- database
variable "postgres_sku" {
  description = "Flexible Server SKU, e.g. B_Standard_B1ms (dev) or GP_Standard_D4ds_v5 (prod)."
  type        = string
}

variable "postgres_storage_mb" {
  type    = number
  default = 32768
}

variable "postgres_high_availability" {
  description = "Zone-redundant hot standby with synchronous replication (automatic failover, RPO 0)."
  type        = bool
  default     = false
}

variable "postgres_read_replica" {
  description = "Create an asynchronous read replica for catalogue reads (Database:ReadReplicaConnectionString)."
  type        = bool
  default     = false
}

variable "postgres_backup_retention_days" {
  type    = number
  default = 7
}

variable "postgres_geo_redundant_backup" {
  type    = bool
  default = false
}

# ---------------- api
variable "api_min_replicas" {
  type    = number
  default = 1
}

variable "api_max_replicas" {
  type    = number
  default = 3
}

variable "api_cpu" {
  type    = number
  default = 0.5
}

variable "api_memory" {
  type    = string
  default = "1Gi"
}

variable "tags" {
  type    = map(string)
  default = {}
}
