# =============================================================================
# Azure Application Gateway v2 (WAF & Ingress Entry)
# =============================================================================

resource "azurerm_public_ip" "appgw_pip" {
  name                = "pip-appgw-${var.name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  allocation_method   = "Static"
  sku                 = "Standard"
  zones               = var.availability_zones

  tags = var.tags
}

locals {
  gateway_ip_config_name = "appgw-ip-config"
  frontend_port_http     = "frontend-port-http"
  frontend_ip_config     = "frontend-ip-config-public"
  backend_pool_name      = "default-backend-pool"
  http_setting_name      = "default-http-setting"
  listener_http_name     = "http-listener"
  request_routing_rule   = "http-routing-rule"
}

resource "azurerm_application_gateway" "appgw" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  zones               = var.availability_zones

  sku {
    name     = "Standard_v2"
    tier     = "Standard_v2"
    capacity = 1
  }

  gateway_ip_configuration {
    name      = local.gateway_ip_config_name
    subnet_id = var.subnet_id
  }

  frontend_port {
    name = local.frontend_port_http
    port = 80
  }

  frontend_ip_configuration {
    name                 = local.frontend_ip_config
    public_ip_address_id = azurerm_public_ip.appgw_pip.id
  }

  backend_address_pool {
    name = local.backend_pool_name
  }

  backend_http_settings {
    name                  = local.http_setting_name
    cookie_based_affinity = "Disabled"
    port                  = 80
    protocol              = "Http"
    request_timeout       = 30
  }

  http_listener {
    name                           = local.listener_http_name
    frontend_ip_configuration_name = local.frontend_ip_config
    frontend_port_name             = local.frontend_port_http
    protocol                       = "Http"
  }

  request_routing_rule {
    name                       = local.request_routing_rule
    rule_type                  = "Basic"
    http_listener_name         = local.listener_http_name
    backend_address_pool_name  = local.backend_pool_name
    backend_http_settings_name = local.http_setting_name
    priority                   = 100
  }

  tags = var.tags
}
