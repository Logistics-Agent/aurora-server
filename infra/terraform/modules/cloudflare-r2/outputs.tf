output "bucket_name" {
  description = "Cloudflare R2 Bucket name"
  value       = cloudflare_r2_bucket.mail_storage.name
}

output "bucket_id" {
  description = "Cloudflare R2 Bucket ID"
  value       = cloudflare_r2_bucket.mail_storage.id
}

output "endpoint_url" {
  description = "S3-compatible R2 endpoint URL"
  value       = "https://${var.account_id}.r2.cloudflarestorage.com"
}

