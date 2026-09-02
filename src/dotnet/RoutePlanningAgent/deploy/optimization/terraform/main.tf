# =============================================================================
# Aurora Route Optimization — Terraform Azure Blob Storage for OSRM Datasets
# =============================================================================
terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.90"
    }
  }
}

provider "azurerm" {
  features {}
}

# 1. Resource Group (Optional / Data Reference if existing)
data "azurerm_resource_group" "rg" {
  count = var.create_resource_group ? 0 : 1
  name  = var.resource_group_name
}

resource "azurerm_resource_group" "rg" {
  count    = var.create_resource_group ? 1 : 0
  name     = var.resource_group_name
  location = var.location

  tags = {
    Environment = var.environment
    Service     = "RoutePlanningAgent-OSRM"
    ManagedBy   = "Terraform"
  }
}

locals {
  rg_name     = var.create_resource_group ? azurerm_resource_group.rg[0].name : data.azurerm_resource_group.rg[0].name
  rg_location = var.create_resource_group ? azurerm_resource_group.rg[0].location : data.azurerm_resource_group.rg[0].location
}

# 2. Azure Storage Account (Standard LRS is optimal for read-heavy map datasets)
resource "azurerm_storage_account" "osrm_storage" {
  name                     = var.storage_account_name
  resource_group_name      = local.rg_name
  location                 = local.rg_location
  account_tier             = "Standard"
  account_replication_type = var.environment == "prod" ? "ZRS" : "LRS"
  account_kind             = "StorageV2"
  min_tls_version          = "TLS1_2"

  enable_https_traffic_only = true
  allow_nested_items_to_be_public = false

  blob_properties {
    versioning_enabled = true
    delete_retention_policy {
      days = 14
    }
  }

  tags = {
    Environment = var.environment
    Service     = "RoutePlanningAgent-OSRM"
    ManagedBy   = "Terraform"
  }
}

# 3. Blob Container for OSRM MLD Map Datasets
resource "azurerm_storage_container" "osrm_container" {
  name                  = var.container_name
  storage_account_name  = azurerm_storage_account.osrm_storage.name
  container_access_type = "private"
}

# 4. User Assigned Managed Identity for OSRM Downloader (AKS Pod / VM)
resource "azurerm_user_assigned_identity" "osrm_identity" {
  name                = "uami-osrm-reader-${var.environment}"
  resource_group_name = local.rg_name
  location            = local.rg_location

  tags = {
    Environment = var.environment
    Service     = "RoutePlanningAgent-OSRM"
    ManagedBy   = "Terraform"
  }
}

# 5. Role Assignment: Storage Blob Data Reader (Least Privilege for OSRM Workload)
resource "azurerm_role_assignment" "osrm_blob_reader" {
  scope                = azurerm_storage_container.osrm_container.resource_manager_id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_user_assigned_identity.osrm_identity.principal_id
}
