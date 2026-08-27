# Intelligent Document OCR & Data Extraction — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & Computer Vision System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in .NET 10 `DocumentOcr` implementation.

---

### Q1 (Junior): What happens when an uploaded Bill of Lading has low image resolution or blurry text?
**Answer**:  
When OCR extraction yields an average confidence score below 0.85 (or any mandatory field has confidence < 0.70), the job status automatically transitions to `RequiresReview`. The document is placed in the staff review queue, where an authorized operator (`ocr:review`) inspects the original scan alongside highlighted bounding boxes and manually corrects the values.

---

### Q2 (Mid): How does Aurora catch hallucinated or misread container numbers without human intervention?
**Answer**:  
Aurora runs a deterministic **ISO 6346 Checksum Validator** on all extracted container numbers. It computes the weighted modulo-11 check digit against the 4-letter prefix and 6-digit serial. If the OCR engine misreads a single digit (e.g. `8` as `B` or `3` as `8`), the check digit test fails, flagging a validation error and routing the document to human review before it can create erroneous customs filings.

---

### Q3 (Mid): How are extracted OCR events communicated to other microservices?
**Answer**:  
The service uses the **Transactional Outbox Pattern**. When a job reaches `Completed` (either automatically or via human review approval), the database transaction commits the status change and writes a `DocumentOcrCompletedEvent` record to the `outbox_messages` table. A background service polls the outbox and publishes the event to RabbitMQ, where `ShipmentWorkflow` ingests the verified document fields.

---

### Q4 (Senior): How is document storage secured in a multi-tenant cloud environment?
**Answer**:  
1. Raw PDFs and cropped images are stored in Cloudflare R2 / S3 using tenant-isolated path hierarchies (`tenants/{tenantId}/documents/...`).
2. The storage bucket is strictly private; public access is disabled.
3. When the staff frontend requests a document preview, the BFF issues a short-lived **Pre-Signed URL** (expiring in 15 minutes) only after verifying the user's tenant ownership and permission token.

---

### Q5 (System Design): What are the tradeoffs of multi-provider OCR fallback vs. a single vendor?
**Answer**:  
- **Pros**: Mitigates vendor outages, avoids single-provider rate limits, and allows cost optimization (e.g. cheaper open-source Tesseract/vLLM for clear text; premium Azure Document Intelligence for complex tabular invoices).
- **Cons**: Requires maintaining normalization adapters for differing provider JSON schemas and managing multiple API keys in the central `AiGovernance` gateway.
