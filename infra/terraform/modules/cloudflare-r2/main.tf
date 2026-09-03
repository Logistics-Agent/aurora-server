# =============================================================================
# Cloudflare R2 Bucket for Mail Service & Storage
# =============================================================================

resource "cloudflare_r2_bucket" "mail_storage" {
  account_id    = var.account_id
  name          = var.bucket_name
}

