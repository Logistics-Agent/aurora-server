# Regulatory Compliance Integration Events

Regulatory Compliance publish kết quả evaluation qua transactional outbox. Event chứa summary consumer-safe, không chứa embedding, retrieval prompt hoặc toàn bộ regulation chunks.

## ComplianceEvaluationCompletedEvent

* Exchange: `RegulatoryCompliance.Contracts.Events:ComplianceEvaluationCompletedEvent`
* Trigger: cited compliance evaluation hoàn tất, kể cả khi evidence insufficient và cần manual review.
* Consumer: Notification.

```json
{
  "eventId": "UUID v7",
  "contractVersion": 1,
  "tenantId": "UUID",
  "evaluationId": "UUID",
  "externalShipmentId": "UUID",
  "externalDocumentIds": ["UUID"],
  "riskLevel": "High",
  "evidenceSufficiency": "Sufficient",
  "complianceConfidence": 0.88,
  "violationCount": 2,
  "missingDocuments": ["PackingList"],
  "summary": "Compliance evaluation completed with cited regulatory evidence.",
  "occurredAt": "RFC3339 UTC"
}
```

## ComplianceEvaluationFailedEvent

* Exchange: `RegulatoryCompliance.Contracts.Events:ComplianceEvaluationFailedEvent`
* Trigger: evaluation đã được tạo nhưng pipeline thất bại và result được persist ở trạng thái Failed.
* Consumer: Notification.

```json
{
  "eventId": "UUID v7",
  "contractVersion": 1,
  "tenantId": "UUID",
  "evaluationId": "UUID",
  "externalShipmentId": "UUID",
  "externalDocumentIds": ["UUID"],
  "errorCode": "EVALUATION_FAILED",
  "errorMessage": "Bounded diagnostic message",
  "summary": "Compliance evaluation failed and requires review.",
  "occurredAt": "RFC3339 UTC"
}
```

## Interpretation rules

* `Completed` không đồng nghĩa shipment compliant; consumer phải đọc `riskLevel`, findings/missing-document summary và evidence sufficiency.
* `complianceConfidence` nằm trong `[0, 1]` và phản ánh coverage/relevance/extraction confidence, không phải xác suất pháp lý tuyệt đối.
* External shipment/document IDs chỉ là references; không có cross-service foreign key.
* Event và evaluation state commit atomically; outbox publisher uses allowlisted types, bounded retry và row locking.
* Event không thay thế API `GetComplianceEvaluation` khi consumer cần findings và citations đầy đủ.

