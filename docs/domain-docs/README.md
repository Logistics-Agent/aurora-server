# BỘ TÀI LIỆU NGHIỆP VỤ LOGISTICS, PHÁP LÝ & HỒ SƠ NỀN TẢNG AURORA

> **Thư mục:** `docs/domain-docs/`  
> **Phiên bản:** 1.1.0  
> **Mục tiêu:** Cung cấp tài liệu đầy đủ, chuẩn hóa về thuật ngữ Logistics, AI, phân loại chứng từ lô hàng (Tenant vs. Platform), cơ sở pháp lý theo từng vi dịch vụ, tài liệu nguồn RAG Vector Store và các mẫu email nghiệp vụ chuẩn giữa Khách hàng và Doanh nghiệp.

---

## Danh mục Tài liệu trong Bộ tài liệu Nghiệp vụ

| STT | Tài liệu | Mô tả chi tiết | Tệp đính kèm |
| :---: | :--- | :--- | :--- |
| **01** | **Bảng thuật ngữ Chuyên ngành Logistics & AI / Công nghệ** | Tổng hợp đầy đủ các thuật ngữ nghiệp vụ Logistics quốc tế (B/L, FCL/LCL, CY, CFS, Incoterms 2020, Demurrage/Detention, HS Code, VGM) và thuật ngữ Công nghệ/AI trong Aurora (OCR, RAG, LLM Agent, Vector Embeddings, CBAC, Multi-tenancy, Outbox). | [01_GLOSSARY_LOGISTICS_AND_AI.md](file:///d:/CD_HTTM/aurora-server/docs/domain-docs/01_GLOSSARY_LOGISTICS_AND_AI.md) |
| **02** | **Danh mục & Đặc tả Chứng từ Vận hành** | Phân định rành mạch giữa **Chứng từ của Lô hàng (Tenant Documents)** như Bill of Lading, Commercial Invoice, Packing List, Tờ khai Hải quan, C/O, POD và **Hồ sơ Chứng từ Cấp Hệ thống (System - Platform Documents)** như Hợp đồng SaaS & SLA, Bảng kê thuê bao nền tảng, Audit Log bảo mật, AI Traceability. | [02_SHIPPING_AND_PLATFORM_DOCUMENTS.md](file:///d:/CD_HTTM/aurora-server/docs/domain-docs/02_SHIPPING_AND_PLATFORM_DOCUMENTS.md) |
| **03** | **Khung Pháp lý & Quy định Chuyên ngành theo từng Service** | Hệ thống luật Hàng hải (Bộ luật HHVN 2015, SOLAS/VGM, Hague-Visby), Luật Giao thông Đường bộ (Nghị định 10/2020/NĐ-CP, GPS Tracking), Luật Hải quan & FTAs (EVFTA, CPTPP), Bảo vệ Dữ liệu Cá nhân (Nghị định 13/2023/NĐ-CP, GDPR) và AI Governance (EU AI Act) ánh xạ vào 12+ Microservices. | [03_REGULATORY_AND_LEGAL_FRAMEWORK.md](file:///d:/CD_HTTM/aurora-server/docs/domain-docs/03_REGULATORY_AND_LEGAL_FRAMEWORK.md) |
| **04** | **Bộ Mẫu Email Chuẩn hóa Trao đổi Client - Company** | 8 mẫu email nghiệp vụ song ngữ chuẩn hóa kèm biến động cho toàn bộ vòng đời: Báo giá cước, Xác nhận booking, Cảnh báo sai lệch chứng từ OCR, Thông báo thông quan hải quan, Cảnh báo rủi ro lộ trình/thời tiết, Hoàn tất giao hàng kèm e-POD, Quyết toán hóa đơn cước, và Tiếp nhận giải quyết khiếu nại. | [04_EMAIL_COMMUNICATION_TEMPLATES.md](file:///d:/CD_HTTM/aurora-server/docs/domain-docs/04_EMAIL_COMMUNICATION_TEMPLATES.md) |
| **05** | **Tài liệu Nguồn RAG Global (Luật Hàng hải, Cảng biển, Hải quan, Incoterms)** | Bộ tài liệu tri thức pháp lý dùng chung cấp Platform (`PLATFORM` scope) phục vụ RAG Ingestion tự động qua `SystemIngestionController` cho toàn bộ các Tenant. | [05_GLOBAL_REGULATORY_RAG_SOURCES.md](file:///d:/CD_HTTM/aurora-server/docs/domain-docs/05_GLOBAL_REGULATORY_RAG_SOURCES.md) |
| **06** | **Tài liệu Nguồn RAG Tenant (SOPs Vận hành, Bồi thường, Biểu phí, Hải quan)** | Bộ tài liệu quy trình vận hành nội bộ (`TENANT` scope) phục vụ Tenant upload qua `KnowledgeSOPs` / `PlatformIngestionController`. | [06_TENANT_INTERNAL_SOPS_AND_POLICIES.md](file:///d:/CD_HTTM/aurora-server/docs/domain-docs/06_TENANT_INTERNAL_SOPS_AND_POLICIES.md) |

---

## Hướng dẫn Tích hợp & Áp dụng
1. **Đối với Đội ngũ Kỹ thuật (Backend / Frontend / AI Engineers):**
   - Sử dụng các schema JSON trong tài liệu số **02** để cấu hình schema cho `DocumentOcr` và `ShipmentWorkflow`.
   - Nạp các điều luật trong tài liệu số **05** vào Vector Database thông qua API `POST /api/v1/system/ingestion/regulatory-sources`.
   - Nạp các quy trình SOPs trong tài liệu số **06** vào Vector Database thông qua API `POST /api/v1/admin/ingestion/knowledge-documents` hoặc giao diện Admin SOPs.
   - Cấu hình các template trong tài liệu số **04** vào mẫu email của `MailService` và kịch bản của `CustomerAssistantService`.
2. **Đối với Đội ngũ Vận hành & Nghiệp vụ (Operations & Product Team):**
   - Sử dụng tài liệu số **01** để chuẩn hóa ngôn ngữ giao tiếp và đào tạo nhân sự.
   - Quản lý và đối soát hồ sơ nền tảng theo định dạng chuẩn trong tài liệu số **02** (Phần II).
