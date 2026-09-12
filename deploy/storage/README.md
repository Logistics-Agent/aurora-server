# Browser upload storage policy

`r2-document-uploads-cors.json` is the branch-managed CORS policy for the private R2/S3-compatible document input bucket.

It permits only first-party Aurora frontend origins to perform browser `PUT` uploads with the headers used by the upload contract:

- `Content-Type`
- `x-amz-meta-content-sha256`

New object keys are tenant-partitioned as
`tenants/{tenantId}/documents/{uploadId}/{fileName}`. OCR artifacts use the
same tenant prefix with `/artifacts/`; the bucket must remain private.

The browser supplies `Content-Length` for a `Blob` upload. The DocumentOcr S3 adapter signs the declared length into the presigned URL; the frontend must not try to set that forbidden header manually. DocumentOcr still re-reads the object, verifies byte count/MIME/hash, and cleans up expired objects after upload.

## Apply

From the repository root, with a Wrangler profile that has permission to edit the target R2 bucket:

```bash
npx wrangler r2 bucket cors set "$R2_BUCKET" \
  --file deploy/storage/r2-document-uploads-cors.json
```

`R2_BUCKET` is the bucket name only. Do not put access keys, signed URLs, or other secrets in this repository or in command output.

## Verify

```bash
npx wrangler r2 bucket cors list "$R2_BUCKET"
```

The returned JSON must contain the five origins above, `PUT`, `Content-Type`, and `x-amz-meta-content-sha256`. A browser preflight should be checked from a real FE origin with `Access-Control-Request-Method: PUT` and the two non-forbidden headers.

This artifact intentionally uses Wrangler instead of changing Terraform resources. The existing Cloudflare provider constraint remains `~> 4.0`; do not upgrade it to provider 5 as part of this change.
