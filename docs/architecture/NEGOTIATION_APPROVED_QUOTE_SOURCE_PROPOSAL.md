# Đề xuất nguồn báo giá được duyệt cho Negotiation

**Trạng thái:** Thiết kế đã được chủ dự án duyệt ngày 2026-10-03. Tài liệu này mô tả quy tắc nghiệp vụ, không phải một báo giá có hiệu lực.

## Nguồn dữ liệu

Billing giữ bản ghi `ShipmentApprovedQuote` trong PostgreSQL. Đây là nguồn duy nhất cho **giá báo ban đầu** (`listPrice`) và **giá sàn thương lượng** (`floorPrice`) của từng shipment. ShipmentWorkflow xác nhận shipment, tenant và khách hàng có thật; Billing kiểm soát người duyệt giá. Negotiation chỉ đọc bản báo giá đã duyệt và lưu ảnh chụp của nó vào phiên thương lượng. Không lưu giá sàn trong tài liệu RAG, email, hoặc phản hồi cho khách hàng.

Giá này dùng **trước khi** hai bên chốt giao dịch. **Giá cuối cùng** phải được nhân viên xác nhận và ghi vào thỏa thuận giá tại Billing. Nhánh triển khai báo giá này không tự tạo thỏa thuận giá cuối cùng; một đề xuất `ACCEPT` từ AI chỉ chuyển sang trạng thái chờ phê duyệt.

## Bản ghi cần lưu

| Trường | Ý nghĩa |
| --- | --- |
| `id`, `revision` | Mã báo giá và số phiên bản; bản đã duyệt không bị sửa tại chỗ. |
| `tenantId`, `shipmentId`, `customerId` | Ràng buộc báo giá với đúng đơn hàng và khách hàng. |
| `currency` | `USD` ở giai đoạn hiện tại. |
| `listPrice` | Giá ban đầu gửi cho khách, lưu bằng `numeric(18,2)`. |
| `floorPrice` | Mức thấp nhất nhân viên cho phép thương lượng, lưu bằng `numeric(18,2)` và chỉ dùng nội bộ. |
| `evidenceReference` | Mã tài liệu/đường dẫn đến căn cứ tính giá được lưu trong hệ thống. |
| `validFrom`, `validUntil` | Khoảng thời gian báo giá còn hiệu lực, theo UTC. |
| `status` | `DRAFT`, `APPROVED`, `SUPERSEDED`, hoặc `REVOKED`. |
| `createdBy`, `createdAt`, `approvedBy`, `approvedAt` | Người lập, người duyệt và thời điểm để kiểm toán. |

## Quy tắc phê duyệt

1. Nhân viên lập báo giá ở trạng thái `DRAFT`. Giá phải dương, `floorPrice <= listPrice`, đúng 2 chữ số thập phân tối đa và đúng shipment/khách hàng của tenant.
2. Người có quyền duyệt giá, khác người lập, xác nhận căn cứ tính giá, thời hạn và cả hai mức giá. Chỉ sau bước này trạng thái mới thành `APPROVED` và có `approvedBy`, `approvedAt`.
3. Với cùng tenant, shipment và khách hàng, tại một thời điểm chỉ có một bản `APPROVED` còn hiệu lực. Thay giá phải tạo revision mới; bản cũ thành `SUPERSEDED`. Thu hồi dùng `REVOKED` cùng lý do và người thực hiện.
4. Negotiation nhận offer thì lấy bản `APPROVED` còn hạn từ Billing qua kết nối nội bộ, đối chiếu tenant/shipment/customer, rồi ghi `quoteId`, `revision`, `evidenceReference`, `approvedBy`, `approvedAt`, `listPrice`, `floorPrice` vào phiên. Nếu không có bản hợp lệ thì không mở phiên mới.
5. Giao diện khách hàng chỉ được thấy `listPrice` hoặc mức đề xuất phản hồi. `floorPrice` và lịch sử duyệt chỉ hiện trong giao diện nhân viên có quyền.

## Quyết định đã duyệt

- Billing là nơi sở hữu báo giá được duyệt như mô tả trên.
- Nhân viên có quyền duyệt giá riêng (đề xuất `billing_settlement:quote:approve`) được phép đưa báo giá từ `DRAFT` sang `APPROVED`, và không được tự duyệt báo giá mình lập.
- Mỗi báo giá áp dụng cho một shipment và một khách hàng, dùng USD. Các loại tiền khác cần quy tắc đổi tiền riêng trước khi mở rộng.

Triển khai bảng dữ liệu, API duyệt giá, kết nối Billing → Negotiation và giao diện nhân viên theo các quyết định này.

## Triển khai cơ sở dữ liệu

- Database Billing mới: chạy `prisma migrate deploy` để tạo schema gốc và các bảng báo giá.
- Database Billing đã có bảng từ `db push`: đối chiếu schema thực tế với migration `20261003090000_initial_billing`. Chỉ sau khi xác nhận chúng khớp, đánh dấu migration gốc bằng `prisma migrate resolve --applied 20261003090000_initial_billing`, rồi chạy `prisma migrate deploy` cho bảng báo giá. Không chạy migration gốc đè lên bảng đang dùng.
- Database Negotiation đã có migration gốc: chạy `prisma migrate deploy`. Migration chặn hai phiên `OPEN`/`PENDING_APPROVAL` cho cùng tenant, shipment, khách hàng; cần xử lý dữ liệu trùng trước khi áp dụng nếu có.
- Nạp cùng `INTERNAL_SERVICE_SECRET` cho Billing, Negotiation và Staff BFF. Negotiation trỏ `BILLING_GRPC_URL` đến Billing gRPC. Không mở phiên nếu Billing không có báo giá đã duyệt còn hạn.
