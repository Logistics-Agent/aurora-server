locals {
  tags = {
    Project     = "Aurora"
    Environment = var.environment
    Layer       = "Core"
    ManagedBy   = "Terraform"
  }
}
