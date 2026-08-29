# Aurora Platform - Staff Exclusive API Catalog (STAFF_ONLY)

> **Document ID:** `DOC-BFF-STAFF`  
> **Status:** Canonical Specification Complete  
> **Scope:** HTTP REST APIs exclusively accessible by the `STAFF` role.  
> **Rule:** Operational APIs accessible by both `STAFF` and `MANAGER` (e.g. Shipment creation, Route editing, Document upload) are defined in [shared-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/shared-api.md).

---

## 1. Staff Exclusive API Table

| Method | Endpoint | Function | Service | RPC | Main Source File |
| :--- | :--- | :--- | :--- | :--- | :--- |
| *None* | *All operational core APIs are shared with Manager* | *Operational Co-Management* | *Multiple* | *Multiple* | [shared-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/shared-api.md) |

---

## 2. Operational Access Model Note

In the Aurora logistics architecture, frontline operational tasks (such as creating shipments, managing cargo lines, uploading documents for OCR, drafting emails, and calculating freight estimates) permit operational supervision by `MANAGER` roles.

Therefore, according to the **Critical Role Organization Rule**, all common operational capabilities are classified as **`SHARED [STAFF, MANAGER]`** or **`SHARED [STAFF, MANAGER, ADMIN]`** and are documented in [docs/bff-api/shared-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/shared-api.md) to avoid endpoint duplication.

Frontline automated rate bidding (`POST /api/v1/negotiation/offer`) is currently classified under [blocked-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/blocked-api.md) pending backend protobuf contract finalization.
