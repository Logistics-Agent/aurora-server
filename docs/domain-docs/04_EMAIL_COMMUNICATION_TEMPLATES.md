# BỘ MẪU EMAIL CHUẨN HÓA GIAO TIẾP KHÁCH HÀNG & DOANH NGHIỆP (CLIENT - COMPANY COMMUNICATION TEMPLATES)

> **Mã tài liệu:** `DOC-AURORA-EMAIL-04`  
> **Phiên bản:** 1.0.0  
> **Tích hợp:** `MailService` (.NET 10), `CustomerAssistantService` (NestJS) và Hộp thư Hợp tác chung (Shared Mailbox) của Aurora Platform.  
> **Quy chuẩn định dạng:** Hỗ trợ song ngữ (Anh - Việt), tích hợp các biến động (`{{variable}}`) để hệ thống tự động điền dữ liệu từ Vận đơn và Hóa đơn.

---

## 1. Quy trình & Biến động Dữ liệu (Template Variables)

| Biến đại diện | Ý nghĩa | Nguồn dữ liệu trong Aurora |
| :--- | :--- | :--- |
| `{{client_name}}` | Tên khách hàng / Đại diện doanh nghiệp | `CustomerAssistant` / `Shipment` |
| `{{company_name}}` | Tên doanh nghiệp Logistics (Tenant) | `IamTenant.TenantConfig` |
| `{{shipment_id}}` | Mã số vận đơn nội bộ Aurora | `ShipmentWorkflow` (`SHP-XXXX`) |
| `{{bl_number}}` | Số vận đơn đường biển / HBL / MBL | `DocumentOcr` |
| `{{origin_port}}` / `{{dest_port}}` | Cảng / Điểm xuất phát và đích đến | `RoutePlanningAgent` |
| `{{eta_date}}` / `{{etd_date}}` | Thời gian dự kiến khởi hành / đến nơi | `RoutePlanningAgent` / `GpsTracking` |
| `{{invoice_amount}}` | Tổng số tiền cần thanh toán | `BillingSettlement` |
| `{{tracking_url}}` | Link tra cứu lộ trình trực tiếp | Cổng tự phục vụ Aurora Portal |

---

## 2. Danh mục 8 Mẫu Email Nghiệp vụ Chuẩn

---

### MẪU 1: BÁO GIÁ CƯỚC VẬN CHUYỂN (FREIGHT QUOTATION OFFER)

- **Ngữ cảnh:** Gửi sau khi khách hàng yêu cầu báo giá hoặc sau khi `NegotiationAgent` chốt biểu phí.
- **Tiêu đề:** `[Báo giá / Quotation] {{company_name}} - Báo giá dịch vụ vận chuyển lô hàng {{origin_port}} đến {{dest_port}} - Ref: {{quote_ref}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách hàng **{{client_name}}**,
> 
> Lời đầu tiên, **{{company_name}}** xin gửi lời chào trân trọng và cảm ơn Quý khách đã quan tâm đến dịch vụ logistics của chúng tôi.
> 
> Căn cứ theo yêu cầu vận chuyển của Quý công ty, chúng tôi trân trọng gửi bảng báo giá cước và phụ phí chi tiết cho tuyến vận chuyển **{{origin_port}}** đi **{{dest_port}}** như sau:
> 
> **1. Chi tiết lô hàng dự kiến:**
> - Mặt hàng: `{{commodity_description}}` (Mã HS: `{{hs_code}}`)
> - Quy cách đóng gói: `{{package_type}}` - Số lượng: `{{quantity}}` - Trọng lượng: `{{gross_weight}} kg` / `{{cbm}} CBM`
> - Điều kiện Incoterms: `{{incoterms}}`
> 
> **2. Chi phí dịch vụ chi tiết:**
> - Cước vận chuyển chính (Ocean/Trucking Freight): **{{freight_rate}} {{currency}}**
> - Phụ phí địa phương tại cảng xuất (Local Charges - POL): **{{pol_charges}} {{currency}}**
> - Phụ phí địa phương tại cảng nhập (Local Charges - POD): **{{pod_charges}} {{currency}}**
> - Phí thủ tục hải quan & Giấy phép chuyên ngành: **{{customs_fee}} {{currency}}**
> - **Tổng chi phí ước tính (chưa VAT):** **{{total_estimated_amount}} {{currency}}**
> 
> **3. Hiệu lực báo giá & Điều khoản:**
> - Báo giá có hiệu lực đến hết ngày: **{{quote_expiry_date}}**.
> - Thời gian vận chuyển dự kiến (Transit time): **{{transit_days}} ngày**.
> 
> Quý khách vui lòng bấm xác nhận trực tiếp qua cổng thông tin hoặc phản hồi email này để đội ngũ của chúng tôi tiến hành giữ chỗ (Booking).
> 
> Trân trọng,  
> **Đội ngũ Vận hành & Báo giá {{company_name}}**  
> Hotline: `{{hotline}}` | Email: `{{ops_email}}`

#### English Version:
> Dear **{{client_name}}**,
> 
> Thank you for choosing **{{company_name}}** as your logistics partner.
> 
> We are pleased to provide you with our official freight quotation for the shipment from **{{origin_port}}** to **{{dest_port}}** under Reference Number **{{quote_ref}}**:
> 
> - **Commodity:** `{{commodity_description}}` (HS Code: `{{hs_code}}`)
> - **Volume / Weight:** `{{quantity}} {{package_type}}` / `{{gross_weight}} KGS` / `{{cbm}} CBM`
> - **Incoterms:** `{{incoterms}}`
> - **Ocean / Inland Freight:** **{{freight_rate}} {{currency}}**
> - **Local Charges & Customs Clearance:** **{{local_charges}} {{currency}}**
> - **Validity:** Until **{{quote_expiry_date}}**
> 
> Please reply to this email or access the booking link below to confirm your shipment.  
> Best regards,  
> **Operations Department - {{company_name}}**

---

### MẪU 2: XÁC NHẬN ĐẶT CHỖ & HƯỚNG DẪN GỬI HÀNG (BOOKING CONFIRMATION & SHIPPING ADVICE)

- **Ngữ cảnh:** Hệ thống cấp mã booking tàu/xe và hướng dẫn khách hàng giao hàng đến kho/cảng.
- **Tiêu đề:** `[Booking Confirmation] Xác nhận đặt chỗ thành công - Mã vận đơn: {{shipment_id}} - Booking No: {{booking_no}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> **{{company_name}}** xin thông báo lô hàng của Quý khách đã được giữ chỗ thành công với các thông tin chi tiết dưới đây:
> 
> - **Mã vận đơn nội bộ:** `{{shipment_id}}`
> - **Số Booking:** `{{booking_no}}`
> - **Hãng tàu / Đơn vị vận chuyển:** `{{carrier_name}}` | Tên tàu & Chuyến: `{{vessel_voyage}}`
> - **Cảng bốc hàng (POL):** `{{origin_port}}` (Thời gian đóng bãi Cut-off/SI: **{{cutoff_time}}**)
> - **Cảng dỡ hàng (POD):** `{{dest_port}}`
> - **Ngày tàu chạy dự kiến (ETD):** `{{etd_date}}`
> - **Ngày tàu đến dự kiến (ETA):** `{{eta_date}}`
> - **Địa điểm nhận vỏ container / Giao hàng kho:** `{{depot_address}}`
> 
> **Lưu ý quan trọng:**
> 1. Quý khách vui lòng hoàn tất việc đóng hàng và bàn giao container tại bãi trước thời hạn **Cut-off Time**.
> 2. Vui lòng gửi bộ chứng từ sơ bộ (Draft B/L, Commercial Invoice, Packing List và VGM) trước **{{si_cutoff_time}}** qua hệ thống để thực hiện thủ tục hải quan kịp thời.
> 
> Quý khách có thể theo dõi tiến độ lô hàng tại: [Cổng theo dõi đơn hàng Aurora]({{tracking_url}})
> 
> Trân trọng,  
> **Phòng Điều phối Vận tải {{company_name}}**

---

### MẪU 3: THÔNG BÁO SAI LỆCH CHỨNG TỪ OCR & YÊU CẦU BỔ SUNG (DOCUMENT DISCREPANCY & SUBMISSION NOTICE)

- **Ngữ cảnh:** Service `DocumentOcr` phát hiện chứng từ bị mờ, sai lệch số liệu trọng lượng/mã hàng hoặc thiếu chứng từ bắt buộc.
- **Tiêu đề:** `[Cảnh báo chứng từ / Document Alert] Yêu cầu bổ sung/chỉnh sửa chứng từ cho lô hàng {{shipment_id}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> Trong quá trình rà soát và xử lý hồ sơ chứng từ tự động cho lô hàng **{{shipment_id}}** (Số B/L: `{{bl_number}}`), hệ thống kiểm soát tuân thủ của chúng tôi ghi nhận một số điểm không khớp như sau:
> 
> **Chi tiết sai lệch phát hiện:**
> - **Loại chứng từ:** `{{discrepancy_doc_type}}` (ví dụ: Commercial Invoice vs Packing List)
> - **Nội dung sai lệch:** `{{discrepancy_details}}` (Ví dụ: *Tổng trọng lượng Gross Weight trên Packing List là 18,450 kg nhưng trên Tờ khai Hải quan là 18,000 kg*).
> - **Tình trạng hình ảnh scan:** `{{scan_quality_status}}`
> 
> **Hành động cần phối hợp:**
> Quý khách vui lòng cung cấp lại bản scan rõ nét hoặc tệp PDF gốc đã chỉnh sửa của chứng từ nêu trên trước **{{doc_deadline}}** để tránh gián đoạn tiến độ mở tờ khai hải quan và phát sinh phí lưu bãi.
> 
> Quý khách có thể tải trực tiếp tài liệu cập nhật tại: [Cập nhật chứng từ lô hàng]({{upload_url}})
> 
> Trân trọng,  
> **Bộ phận Kiểm soát Chứng từ & Tuân thủ {{company_name}}**

---

### MẪU 4: THÔNG BÁO KẾT QUẢ THÔNG QUAN HẢI QUAN (CUSTOMS CLEARANCE STATUS)

- **Ngữ cảnh:** Hệ thống ghi nhận kết quả phân luồng và thông quan từ cơ quan Hải quan.
- **Tiêu đề:** `[Hải quan / Customs Notice] Cập nhật tình trạng thông quan lô hàng {{shipment_id}} - Luồng: {{customs_channel}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> **{{company_name}}** xin trân trọng thông báo tình trạng làm thủ tục hải quan cho lô hàng **{{shipment_id}}** tại Chi cục Hải quan `{{customs_office}}`:
> 
> - **Số tờ khai:** `{{declaration_number}}`
> - **Kết quả phân luồng:** **{{customs_channel}}** (Luồng Xanh / Vàng / Đỏ)
> - **Trạng thái thông quan:** **{{clearance_status}}** (ĐÃ THÔNG QUAN / ĐANG KIỂM HÓA)
> - **Thời điểm phê duyệt:** `{{cleared_timestamp}}`
> 
> *(Nếu Luồng Đỏ / Kiểm hóa chuyên ngành):* Đội ngũ hiện trường của chúng tôi đang phối hợp cùng công chức hải quan thực hiện thủ tục kiểm tra thực tế hàng hóa tại bãi. Dự kiến hoàn tất trong vòng `{{estimated_inspection_hours}} giờ`.
> 
> Lô hàng hiện đã đủ điều kiện để tiến hành xếp lên phương tiện vận chuyển và tiếp tục hành trình giao hàng.
> 
> Trân trọng,  
> **Bộ phận Khai báo Hải quan {{company_name}}**

---

### MẪU 5: CẢNH BÁO RỦI RO LỘ TRÌNH & CHẬM TRỄ PHÁT SINH (ROUTE RISK & DELAY EXCEPTION ALERT)

- **Ngữ cảnh:** Thuật toán `RoutePlanningAgent` hoặc `GpsTracking` phát hiện thời tiết xấu, tắc đường cảng, hoặc sự cố xe làm thay đổi ETA.
- **Tiêu đề:** `[Cảnh báo vận hành / Exception Alert] Cập nhật thời gian giao hàng dự kiến lô hàng {{shipment_id}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> Hệ thống giám sát vận tải thông minh của **{{company_name}}** xin gửi thông báo cập nhật về hành trình vận chuyển của lô hàng **{{shipment_id}}**:
> 
> - **Phương tiện vận chuyển / Biển số xe:** `{{vehicle_plate}}`
> - **Vị trí hiện tại:** `{{current_gps_location}}`
> - **Lý do điều chỉnh:** `{{delay_reason}}` (Ví dụ: *Ùn tắc giao thông cục bộ tại cửa ngõ Cảng Cát Lái / Ảnh hưởng bão thời tiết đường biển*).
> - **Thời gian đến ban đầu (Original ETA):** `{{original_eta}}`
> - **Thời gian đến điều chỉnh mới (Updated ETA):** **{{updated_eta}}** (Chậm hơn dự kiến `{{delay_duration}}`).
> 
> Đội ngũ điều phối của chúng tôi đang tích cực tối ưu tuyến đường vòng và làm việc với các đơn vị liên quan để bàn giao hàng trong thời gian sớm nhất có thể.
> 
> Theo dõi trực tiếp vị trí phương tiện tại: [Bản đồ hành trình GPS]({{gps_tracking_url}})
> 
> Trân trọng,  
> **Trung tâm Giám sát Vận hành GPS {{company_name}}**

---

### MẪU 6: THÔNG BÁO GIAO HÀNG THÀNH CÔNG KÈM BIÊN BẢN POD (PROOF OF DELIVERY NOTICE)

- **Ngữ cảnh:** Tài xế hoàn tất việc dỡ hàng tại kho đích và người nhận ký xác nhận biên bản POD điện tử.
- **Tiêu đề:** `[Hoàn tất giao hàng / Delivered] Bàn giao thành công lô hàng {{shipment_id}} - Kèm chứng từ POD`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> **{{company_name}}** vui mừng thông báo lô hàng của Quý khách đã được giao an toàn và bàn giao đầy đủ tới người nhận:
> 
> - **Mã vận đơn:** `{{shipment_id}}`
> - **Địa điểm giao hàng:** `{{delivery_address}}`
> - **Thời gian hoàn tất giao nhận:** `{{delivery_timestamp}}`
> - **Người đại diện ký nhận:** `{{receiver_name}}` (Chức vụ: `{{receiver_title}}`)
> - **Tình trạng hàng hóa khi bàn giao:** `{{cargo_condition}}` (Nguyên đai, nguyên kiện, chì niêm phong còn nguyên vẹn).
> 
> Chứng từ giao hàng điện tử (e-POD) cùng hình ảnh chụp tại hiện trường đã được đính kèm trong email này và lưu trữ trên hệ thống của Quý khách.
> 
> Tải tệp e-POD có chữ ký: [Tải chứng từ POD]({{pod_download_url}})
> 
> Trân trọng cảm ơn Quý khách đã tin tưởng và đồng hành cùng **{{company_name}}**.  
> **Bộ phận Dịch vụ Khách hàng {{company_name}}**

---

### MẪU 7: QUYẾT TOÁN HÓA ĐƠN CƯỚC & ĐỀ NGHỊ THANH TOÁN (FREIGHT INVOICE & SETTLEMENT)

- **Ngữ cảnh:** Service `BillingSettlement` xuất hóa đơn cước và bảng kê chi phí chính thức sau khi hoàn tất đơn hàng.
- **Tiêu đề:** `[Hóa đơn thanh toán / Invoice] Thông báo quyết toán cước phí lô hàng {{shipment_id}} - Hóa đơn No: {{invoice_no}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> **{{company_name}}** xin gửi tới Quý khách bảng quyết toán chi phí và hóa đơn điện tử cho lô hàng **{{shipment_id}}** đã hoàn tất:
> 
> **1. Bảng kê chi phí thanh toán:**
> - Cước vận chuyển: `{{freight_fee}} {{currency}}`
> - Phí nâng hạ và lưu bãi cảng (nếu có): `{{port_storage_fee}} {{currency}}`
> - Phí thủ tục hải quan: `{{customs_fee}} {{currency}}`
> - Thuế GTGT (VAT): `{{vat_amount}} {{currency}}`
> - **Tổng số tiền thanh toán:** **{{total_due_amount}} {{currency}}**
> 
> **2. Thông tin thanh toán:**
> - Tên tài khoản: `{{company_bank_account_name}}`
> - Số tài khoản: `{{bank_account_number}}` tại `{{bank_name}}`
> - Nội dung chuyển khoản: `THANH TOAN HOA DON {{invoice_no}} {{shipment_id}}`
> - Hạn chót thanh toán: **{{payment_due_date}}**
> 
> *(Nếu thanh toán qua Ví ký quỹ Escrow Aurora):* Số tiền sẽ được hệ thống tự động đối soát và giải ngân theo thỏa thuận hạn mức tín dụng.
> 
> Tải hóa đơn điện tử (PDF/XML): [Tải Hóa đơn VAT]({{invoice_download_url}})
> 
> Trân trọng,  
> **Phòng Tài chính Kế toán {{company_name}}**

---

### MẪU 8: TIẾP NHẬN & XỬ LÝ KHIẾU NẠI TỔN THẤT HÀNG HÓA (CARGO CLAIM & INCIDENT RESOLUTION)

- **Ngữ cảnh:** Phát sinh rủi ro hàng hóa bị hư hại, ướt nước, hoặc thiếu kiện cần lập biên bản bồi thường bảo hiểm.
- **Tiêu đề:** `[Tiếp nhận khiếu nại / Claim Resolution] Xác nhận tiếp nhận hồ sơ sự cố lô hàng {{shipment_id}}`

#### Nội dung Tiếng Việt:
> Kính gửi Quý khách **{{client_name}}**,
> 
> **{{company_name}}** chân thành xin lỗi vì sự bất tiện mà Quý khách gặp phải liên quan đến sự cố phát sinh của lô hàng **{{shipment_id}}** (Số B/L: `{{bl_number}}`).
> 
> Chúng tôi đã lập hồ sơ giải quyết khiếu nại mã số **`{{claim_case_id}}`** và chỉ định chuyên viên xử lý riêng biệt:
> 
> - **Mô tả sự cố ghi nhận:** `{{incident_description}}`
> - **Biên bản hiện trường (ROROC/Survey Report):** Đã lập lúc `{{incident_timestamp}}` với sự chứng kiến của các bên.
> - **Chuyên viên phụ trách:** `{{claims_officer_name}}` - Hotline: `{{claims_officer_phone}}`
> - **Quy trình xử lý tiếp theo:**
>   1. Bộ phận Giám định Bảo hiểm tiến hành định giá mức độ tổn thất thực tế trong vòng 48 giờ.
>   2. Đối chiếu điều khoản bảo hiểm vận tải và công ước hàng hải quốc tế áp dụng.
>   3. Đưa ra phương án bồi thường thỏa đáng trước ngày **{{claim_resolution_deadline}}**.
> 
> Chúng tôi cam kết bảo vệ quyền lợi tối đa của Quý khách theo đúng hợp đồng dịch vụ đã ký kết.
> 
> Trân trọng,  
> **Phòng Pháp chế & Bồi thường Bảo hiểm {{company_name}}**

---
*Tài liệu chuẩn hóa phục vụ tự động hóa truyền thông trên nền tảng Aurora.*
