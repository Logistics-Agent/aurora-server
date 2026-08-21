# Document OCR Integration Events

Document OCR publish terminal job result qua transactional outbox. Object storage content không được nhúng vào event.

## DocumentOcrCompletedEvent

* Exchange: `DocumentOcr.Contracts.Events:DocumentOcrCompletedEvent`
* Trigger: OCR provider hoàn tất extraction và normalized result được persist.
* Consumer: Notification. Các service cần structured document data có thể subscribe bằng contract riêng hoặc event hiện tại sau compatibility review.

```json
{
  "eventId": "UUID v7",
  "contractVersion": 1,
  "tenantId": "UUID",
  "jobId": "UUID",
  "externalDocumentId": "UUID or null",
  "externalShipmentId": "UUID or null",
  "detectedDocumentType": "CommercialInvoice",
  "normalizedJson": "{...}",
  "confidence": 0.98,
  "needsReview": false,
  "occurredAt": "RFC3339 UTC"
}
```

## DocumentOcrFailedEvent

* Exchange: `DocumentOcr.Contracts.Events:DocumentOcrFailedEvent`
* Trigger: job gặp permanent/invalid/unsupported failure hoặc hết transient retry.
* Consumer: Notification.

```json
{
  "eventId": "UUID v7",
  "contractVersion": 1,
  "tenantId": "UUID",
  "jobId": "UUID",
  "externalDocumentId": "UUID or null",
  "externalShipmentId": "UUID or null",
  "errorCode": "document_processing_failed",
  "errorMessage": "Bounded provider-safe error",
  "occurredAt": "RFC3339 UTC"
}
```

## Security and compatibility

* `NormalizedJson` có thể chứa dữ liệu nghiệp vụ nhạy cảm; consumer chỉ persist khi thực sự sở hữu use case đó.
* Notification không copy `NormalizedJson` vào notification body hoặc log.
* Event không chứa binary file, storage credentials, provider credentials hoặc tenant do client tự chọn.
* Confidence nằm trong `[0, 1]`; low-confidence result có `needsReview = true` theo policy.
* Job terminal state và outbox message commit atomically; publisher dùng explicit event allowlist và bounded retry.

