# Intelligent Document OCR & Data Extraction — Service Overview

> **Service Layer**: Computer Vision, Document Extraction & Human Review  
> **Target Audience**: Technical Recruiters, Computer Vision Engineers, System Architects  
> **Source-of-Truth**: `src/dotnet/DocumentOcr`, `DocumentOcrJob`, `DocumentOcrGrpcService`, `DocumentOcrValidation`, `protos/document_ocr.proto`.

---

## 1. Service Purpose & Problem Solved

Logistics operations deal with hundreds of physical and scanned PDF documents per shipment (Bills of Lading, Commercial Invoices, Packing Lists, Customs Declarations, POD receipts). Manual data entry causes high labor costs, data errors, and delayed shipments. Naive OCR systems produce hallucinated numbers and fail on low-resolution scans without clear validation.

The **Document OCR Service** provides **AI-Driven Document Extraction + Deterministic Schema Validation + Human Review (HitL)**:
- **Multi-Type Logistics Ingestion**: Automated recognition for Bills of Lading (BL), Commercial Invoices, Packing Lists, and Delivery Receipts.
- **Field-Level Confidence Scoring**: Every extracted field (Container No, Seal No, Weight, Currency, Shipper, Consignee) has a normalized confidence score $[0.0, 1.0]$.
- **Deterministic Rules & Anomaly Detection**: Validates extracted container numbers against ISO 6346 checksums, checks invoice arithmetic, and detects date mismatches.
- **Human-in-the-Loop Review Queue**: Low-confidence or invalid documents are automatically routed to the Review Queue (`ocr:review`) for staff verification before shipment processing.

---

## 2. Architecture & Tech Stack

```
[ ShipmentWorkflow / Document Upload / BFF ]
                      │
                      ▼ (gRPC Port 5006)
┌─────────────────────────────────────────────────────────────┐
│                 Document OCR Microservice (.NET 10)         │
│  ├── Document Ingestion & Image Pre-processing              │
│  ├── Multi-Provider OCR Adapter (Azure / Textract / Local)  │
│  ├── Field Extractor & Layout Normalizer                    │
│  ├── Deterministic Validation Engine (ISO 6346 Checksums)   │
│  ├── Human Review & Field Correction Workflow               │
│  └── Transactional Outbox (RabbitMQ Event Publisher)        │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]        [ Cloudflare R2 / S3 ]
     (Jobs, Validations, Fields)       (Raw PDFs & Image Crops)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Persistence** | Entity Framework Core 10, PostgreSQL 16 (Neon Serverless SSL) |
| **File Storage** | Cloudflare R2 / AWS S3 (Signed URLs for high-resolution images) |
| **Events & Messaging** | Transactional Outbox Pattern, RabbitMQ (`DocumentOcrCompletedEvent`) |
| **BFF Client** | `Staff.Bff` (`POST /api/v1/ocr/upload`, `POST /api/v1/ocr/{id}/review`) |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`DocumentOcrJobs`**: Tracks `TenantId`, `DocumentType`, `FileStoragePath`, `Status` (`Queued`, `Processing`, `Completed`, `RequiresReview`, `Failed`), `OverallConfidence`, and extracted JSON payload.
- **`OcrProviderAttempts`**: Logs provider latency, raw JSON, attempt timestamps, and provider names.
- **`DocumentOcrValidations`**: Field-by-field validation results, rule names (e.g. `ISO6346_CONTAINER_CHECKSUM`), severity (`Warning`, `Error`), and corrected values.

---

## 4. API & Contract Surface

Exposed via `protos/document_ocr.proto` (`DocumentOcrService`):
- `SubmitDocumentForOcr`: Submits document binary or R2 URL for asynchronous OCR pipeline processing.
- `GetDocumentOcrJob`: Queries job status, confidence scores, extracted key-value pairs, and validation issues.
- `ReviewDocumentOcrJob`: Human review API allowing authorized staff (`ocr:review`) to correct and finalize extracted fields.
- `ListDocumentOcrJobs`: Filterable query by status, date range, and document type.

---

## 5. Security & Invariants

1. **Deterministic ISO Validation**: Extracted container numbers must pass ISO 6346 check digit algorithms; failures force `RequiresReview` status.
2. **Review Privilege Gate**: Only users with explicit capability `ocr:review` can approve or edit OCR results.
3. **Current Maturity**: Production-ready core extraction and review pipeline with transactional outbox event publishing.
