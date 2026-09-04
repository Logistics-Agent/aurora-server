output "user_pool_id" {
  description = "System Cognito User Pool ID"
  value       = aws_cognito_user_pool.system.id
}

output "user_pool_arn" {
  description = "System Cognito User Pool ARN"
  value       = aws_cognito_user_pool.system.arn
}

output "user_pool_endpoint" {
  description = "System Cognito User Pool Endpoint"
  value       = aws_cognito_user_pool.system.endpoint
}

output "client_ids" {
  description = "Map of App Client IDs (system, admin, staff)"
  value       = { for k, v in aws_cognito_user_pool_client.clients : k => v.id }
  sensitive   = true
}

output "client_secrets" {
  description = "Map of App Client Secrets (system, admin, staff)"
  value       = { for k, v in aws_cognito_user_pool_client.clients : k => v.client_secret }
  sensitive   = true
}

output "system_client_id" {
  description = "System App Client ID for System.Bff"
  value       = lookup(aws_cognito_user_pool_client.clients, "system", null) != null ? aws_cognito_user_pool_client.clients["system"].id : null
  sensitive   = true
}

output "admin_client_id" {
  description = "Admin App Client ID for Admin.Bff"
  value       = lookup(aws_cognito_user_pool_client.clients, "admin", null) != null ? aws_cognito_user_pool_client.clients["admin"].id : null
  sensitive   = true
}

output "staff_client_id" {
  description = "Staff App Client ID for Staff.Bff"
  value       = lookup(aws_cognito_user_pool_client.clients, "staff", null) != null ? aws_cognito_user_pool_client.clients["staff"].id : null
  sensitive   = true
}

output "domain_prefix" {
  description = "Cognito User Pool domain prefix"
  value       = length(aws_cognito_user_pool_domain.main) > 0 ? aws_cognito_user_pool_domain.main[0].domain : null
}

output "iam_user_name" {
  description = "IAM user name for IamTenant"
  value       = aws_iam_user.iam_tenant_user.name
}

output "iam_user_arn" {
  description = "IAM user ARN for IamTenant"
  value       = aws_iam_user.iam_tenant_user.arn
}

output "iam_access_key_id" {
  description = "IAM Access Key ID for IamTenant service"
  value       = aws_iam_access_key.iam_tenant_key.id
}

output "iam_secret_access_key" {
  description = "IAM Secret Access Key for IamTenant service"
  value       = aws_iam_access_key.iam_tenant_key.secret
  sensitive   = true
}
