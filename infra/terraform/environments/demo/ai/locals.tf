locals {
  tags = {
    Project     = "Aurora"
    Environment = var.environment
    Layer       = "AI"
    ManagedBy   = "Terraform"
  }
}
