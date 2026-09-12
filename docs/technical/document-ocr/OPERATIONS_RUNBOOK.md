# Document OCR Operations Runbook

## Incident: staging Document OCR unavailable / HTTP 500

**Environment:** AKS AI demo cluster, namespace `aurora-ai`  
**Service:** `document-ocr`  
**Affected endpoint:** `GET /api/v1/documents/shipment-documents`

### Observed failure chain

The incident had three separate deployment/data issues:

1. The service initially failed to start because
   `Storage:InputBridge:SigningKey` was shorter than the minimum 32 bytes.
2. After the service started, the database was missing columns expected by the
   deployed model. The migration history also contained a duplicate migration
   that attempted to add columns already created by an earlier migration.
3. After the schema was usable, `ListDocuments` failed because the process runs
   as UID `10001` and the image's `/app/storage` directory was not writable.

The final API response was HTTP 500 because the gRPC exception from Document OCR
was mapped to the generic BFF internal-server-error response.

## Temporary fixes applied

### 1. Runtime signing key

A random key was injected directly into the live `document-ocr` Deployment so
the service could start:

```bash
kubectl -n aurora-ai set env deployment/document-ocr \
  Storage__InputBridge__SigningKey="$(openssl rand -base64 48)"
```

This is a runtime hotfix only. The key must be stored in Azure Key Vault and
loaded through External Secrets before treating the deployment as production
ready. Do not commit the generated value to Git.

### 2. Database migration alignment

The migration `20260910000000_AddDocumentContentAndReview` duplicated columns
already created by `20260831155309_AlignDocumentOcrModel`. After confirming the
columns existed, the duplicate migration was marked as applied and the
remaining migrations were executed.

The manual repair statements were:

```sql
UPDATE public."document_ocr_jobs"
SET "ExtractionMode" = 'Structured'
WHERE "ExtractionMode" IS NULL
   OR btrim("ExtractionMode") = '';
```

```sql
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260910000000_AddDocumentContentAndReview', '10.0.9')
ON CONFLICT ("MigrationId") DO NOTHING;
```

This must not be repeated blindly on another database. First compare the table
schema and `__EFMigrationsHistory`; a migration should only be marked applied
when its resulting schema is already present.

### 3. Writable filesystem hotfix

The live Deployment was patched with a writable temporary volume:

```bash
kubectl --context aks-ai-demo -n aurora-ai patch deployment document-ocr \
  --type='strategic' \
  -p '{"spec":{"template":{"spec":{"containers":[{"name":"aurora-service","volumeMounts":[{"name":"document-ocr-storage","mountPath":"/app/storage"}]}],"volumes":[{"name":"document-ocr-storage","emptyDir":{}}]}}}}'
```

The corresponding Helm values are in
`src/dotnet/DocumentOcr/deploy/helm/values.yaml` so a future deployment keeps
the permission fix. The pod was rolled out successfully and the
`UnauthorizedAccessException` for `/app/storage` stopped.

## Current limitations

`emptyDir` is ephemeral. Uploaded inputs and generated OCR artifacts can be
lost when the pod is restarted or rescheduled, while their database records
remain. This is acceptable only as a staging/demo workaround.

The application currently uses filesystem storage for OCR artifacts. The input
storage abstraction has an S3-compatible implementation, but artifact storage
still needs an object-storage implementation before `emptyDir` can be removed.

The `libgssapi_krb5.so.2` startup warning was observed separately. It did not
crash the pod or produce the `/app/storage` exception, but should be resolved
in the container image or dependency configuration before production rollout.

## Production follow-up

1. Create and rotate the input-bridge signing key in Azure Key Vault.
2. Add the Key Vault secret to the Document OCR ExternalSecret mapping.
3. Implement and configure Azure Blob/R2 storage for both uploaded inputs and
   OCR artifacts.
4. Migrate any files currently stored under the pod filesystem.
5. Verify upload, list, retry, download/review, and pod-reschedule behavior.
6. Remove the `emptyDir` mount only after object storage is verified.

