# =============================================================================
# AWS Cognito User Pool, App Client, Domain & IAM for IamTenant Service
# =============================================================================

# 1. Root / System Cognito User Pool
resource "aws_cognito_user_pool" "system" {
  name = var.user_pool_name

  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length                   = 8
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = true
    require_uppercase                = true
    temporary_password_validity_days = 7
  }

  admin_create_user_config {
    allow_admin_create_user_only = false
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  tags = var.tags
}

# 2. System App Client (Used by BFF and IamTenant)
resource "aws_cognito_user_pool_client" "system_client" {
  name         = var.client_name
  user_pool_id = aws_cognito_user_pool.system.id

  generate_secret = var.generate_client_secret

  explicit_auth_flows = [
    "ALLOW_ADMIN_USER_PASSWORD_AUTH",
    "ALLOW_CUSTOM_AUTH",
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_USER_SRP_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH"
  ]

  prevent_user_existence_errors = "ENABLED"
  enable_token_revocation       = true

  access_token_validity  = 60
  id_token_validity      = 60
  refresh_token_validity = 30

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }
}

# 3. Cognito Domain (Optional prefix for Hosted UI / OAuth2)
resource "aws_cognito_user_pool_domain" "main" {
  count        = var.domain_prefix != null && var.domain_prefix != "" ? 1 : 0
  domain       = var.domain_prefix
  user_pool_id = aws_cognito_user_pool.system.id
}

# 4. IAM Least-Privilege Policy for IamTenant Service
# Grants permissions to create & manage tenant User Pools and Admin operations
resource "aws_iam_policy" "iam_tenant_policy" {
  name        = "${var.iam_user_name}-cognito-policy"
  description = "Least privilege policy for IamTenant microservice to manage multi-tenant Cognito pools"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "IamTenantUserPoolProvisioning"
        Effect = "Allow"
        Action = [
          "cognito-idp:CreateUserPool",
          "cognito-idp:CreateUserPoolClient"
        ]
        Resource = "*"
      },
      {
        Sid    = "IamTenantUserPoolInspection"
        Effect = "Allow"
        Action = [
          "cognito-idp:DescribeUserPool",
          "cognito-idp:DescribeUserPoolClient"
        ]
        Resource = "arn:aws:cognito-idp:${var.aws_region}:*:userpool/*"
      },
      {
        Sid    = "IamTenantUserAdministration"
        Effect = "Allow"
        Action = [
          "cognito-idp:AdminCreateUser",
          "cognito-idp:AdminGetUser",
          "cognito-idp:AdminUpdateUserAttributes",
          "cognito-idp:AdminSetUserPassword",
          "cognito-idp:AdminDeleteUser",
          "cognito-idp:AdminDisableUser",
          "cognito-idp:AdminEnableUser"
        ]
        Resource = "arn:aws:cognito-idp:${var.aws_region}:*:userpool/*"
      }
    ]
  })

  tags = var.tags
}

# 5. IAM User for IamTenant Service
resource "aws_iam_user" "iam_tenant_user" {
  name = var.iam_user_name
  tags = var.tags
}

# 6. Attach Policy to IAM User
resource "aws_iam_user_policy_attachment" "iam_tenant_attachment" {
  user       = aws_iam_user.iam_tenant_user.name
  policy_arn = aws_iam_policy.iam_tenant_policy.arn
}

# 7. IAM Access Key for IamTenant Pod / Workload authentication
resource "aws_iam_access_key" "iam_tenant_key" {
  user = aws_iam_user.iam_tenant_user.name
}

