locals {
  tags = {
    Project     = "Aurora"
    Environment = var.environment
    Layer       = "Shared"
    ManagedBy   = "Terraform"
  }
}
