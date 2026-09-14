# TÀI LIỆU RAG QUY TRÌNH NỘI BỘ: SOPS VẬN HÀNH, BỒI THƯỜNG, HẢI QUAN & CHÍNH SÁCH DOANH NGHIỆP (TENANT-LEVEL)

> **Mã tài liệu:** `RAG-SOURCE-TENANT-01`  
> **Phạm vi áp dụng (Visibility):** `TENANT` (Bảo mật nội bộ của từng Công ty Logistics/Forwarder, phân tách theo Tenant ID)  
> **Cơ quan quản lý dữ liệu:** Tenant Admin / Logistics Operation Manager  
> **Mục tiêu RAG:** Cung cấp tri thức nghiệp vụ nội bộ để AI Customer Assistant & Operations Copilot trả lời khách hàng, hướng dẫn nhân viên mới và tự động hóa xử lý sự cố.

---

# PHẦN 1: QUY TRÌNH VẬN HÀNH TIÊU CHUẨN (OPERATIONAL SOPS)

## Quy trình 1: SOP-OPS-01: Quy trình Đặt chỗ (Booking) & Khai báo Trọng lượng (VGM/SI)
- **Mục đích:** Đảm bảo toàn bộ lô hàng xuất nhập khẩu được đặt chỗ với Hãng tàu (Carrier) đúng lịch trình và không bị rớt tàu do quá hạn Cut-off Time.
- **Các bước thực hiện:**
  1. **Tiếp nhận Booking Request từ khách hàng:** Kiểm tra chủng loại hàng hóa, mã HS Code, kích thước container (20ft GP, 40ft GP, 40ft HC, 40ft RF), nhiệt độ bảo quản (đối với hàng lạnh), ngày sẵn sàng hàng (Cargo Ready Date).
  2. **Yêu cầu cấp Booking Note từ Hãng tàu:** Đối chiếu Depot cấp vỏ container rỗng, thời hạn lấy vỏ, thời gian mở bãi cảng (CY Open) và thời hạn đóng bãi (CY Closing / Port Cut-off).
  3. **Thu thập & Nộp Shipping Instruction (SI):**
     - Shipper phải gửi bản SI chính xác trước thời hạn **SI Cut-off tối thiểu 04 tiếng**.
     - Thông tin bắt buộc: Shipper, Consignee, Notify Party, Cảng bốc (POL), Cảng dỡ (POD), Mô tả hàng hóa, Shipping Marks, Số lượng kiện, Trọng lượng Gross Weight, Thể tích CBM.
  4. **Khai báo Phiếu cân VGM:**
     - Lấy dữ liệu từ phiếu cân điện tử của bãi đóng hàng hoặc phiếu cân trạm xe.
     - Truyền dữ liệu VGM lên cổng điện tử Hãng tàu / Cảng biển chậm nhất **06 tiếng trước Closing Time**.

## Quy trình 2: SOP-DOC-02: Quy trình Phát hành Vận đơn House Bill of Lading (HBL)
- **Mục đích:** Chuẩn hóa quy trình phát hành vận đơn thứ cấp của Forwarder, kiểm soát rủi ro giao hàng không cần xuất trình vận đơn gốc.
- **Nguyên tắc phát hành:**
  1. **Original B/L (Vận đơn gốc - Bộ 3/3):** Chỉ phát hành khi khách hàng đã thanh toán đủ 100% cước phí trả trước (Freight Prepaid) và các khoản phụ phí liên quan.
  2. **Surrendered B/L / Telex Release (Vận đơn nộp lại / Điện giao hàng):**
     - Chỉ thực hiện khi có văn bản yêu cầu chính thức (Surrender Request Form) có chữ ký đại diện pháp luật và đóng dấu công ty của Shipper.
     - Phải thu hồi đầy đủ trọn bộ 3 bản Original B/L trước khi gửi điện Telex Release cho đại lý cảng đích (Dest Agent).
  3. **Sea Waybill (Giấy gửi hàng đường biển):** Áp dụng cho các khách hàng uy tín cao, có hợp đồng tín dụng công nợ (Credit Agreement) hoặc các giao dịch nội bộ công ty mẹ - con.
  4. **Switch B/L (Đổi vận đơn trong thương mại 3 bên):**
     - Chỉ được phát hành sau khi thu hồi trọn bộ HBL thứ nhất.
     - Nghiêm cấm thay đổi các thông tin thực tế: Cảng bốc, Cảng dỡ, Số lượng kiện, Trọng lượng hàng hóa và Mô tả hàng hóa nguy hiểm.

---

# PHẦN 2: THỦ TỤC HẢI QUAN & XỬ LÝ KIỂM HÓA (CUSTOMS CLEARANCE SOPS)

## Quy trình 3: SOP-CUS-03: Quy trình Xử lý Tờ khai Hải quan Điện tử VNACCS
- **Mục đích:** Tối ưu hóa thời gian thông quan hàng hóa xuất nhập khẩu, giảm thiểu chi phí lưu bãi lưu xe.
- **Quy trình xử lý theo từng luồng phân loại:**
  1. **Xử lý Tờ khai Luồng Xanh (Thông quan tự động):**
     - In phiếu tiếp nhận tờ khai và mã vạch (Barcode) từ hệ thống VNACCS.
     - Nộp thuế nhập khẩu/VAT điện tử qua cổng kết nối ngân hàng thương mại 24/7.
     - Thực hiện thanh lý giám sát hải quan tại cổng cảng/kho bãi và in phiếu xuất kho (E-EIR).
  2. **Xử lý Tờ khai Luồng Vàng (Kiểm tra hồ sơ điện tử):**
     - Đính kèm hồ sơ điện tử (V5) lên hệ thống Hải quan trong vòng **02 giờ làm việc** kể từ khi phân luồng: Hóa đơn (Commercial Invoice), Vận đơn (B/L), Packing List, C/O, Giấy phép nhập khẩu.
     - Trực tiếp liên hệ công chức Hải quan thụ lý để giải trình trị giá hải quan, mô tả kỹ thuật hàng hóa nếu có nghi vấn tham vấn giá.
  3. **Xử lý Tờ khai Luồng Đỏ (Kiểm tra thực tế hàng hóa):**
     - Đăng ký lịch kiểm hóa với Tổ kiểm hóa chi cục Hải quan cửa khẩu.
     - Điều động nhân viên hiện trường (Field Operations) liên hệ bãi cảng hạ bãi bốc container, mở niêm phong Seal trước sự chứng kiến của công chức Hải quan và đại diện chủ hàng.
     - Sau khi kiểm tra đạt yêu cầu, nhận biên bản chứng nhận kiểm hóa đạt chuẩn và xin niêm phong kẹp chì mới (Customs Seal).

---

# PHẦN 3: XỬ LÝ SỰ CỐ, KHIẾU NẠI & ĐỀN BÙ (DISPUTE & DAMAGE CLAIM SOPS)

## Quy trình 4: SOP-CLM-04: Quy trình Tiếp nhận & Xử lý Khiếu nại Hàng hóa Hư hỏng / Mất mát
- **Mục đích:** Bảo vệ quyền lợi hợp pháp của Doanh nghiệp và Khách hàng, xác định đúng bên chịu trách nhiệm và hoàn tất bồi thường theo đúng luật định.
- **Trình tự các bước xử lý:**
  1. **Ghi nhận sự cố tại Hiện trường (Site Inspection):**
     - Ngay khi phát hiện bao bì rách vỡ, ướt nước, méo mó hoặc Seal bị đứt tại cảng/kho, nhân viên hiện trường phải dừng ngay việc dỡ hàng.
     - Lập tức chụp ảnh/quay video hiện trường: Toàn cảnh container, góc chụp cận số Seal, vị trí hư hỏng và nhãn mác kiện hàng.
     - Lập ngay **Biên bản hàng hóa thừa thiếu/hư hỏng (Cargo Outturn Report - COR)** hoặc Biên bản giao nhận hàng hư hỏng có chữ ký xác nhận của Cảng, Lái xe và Hải quan giám sát.
  2. **Gửi Thông báo Khiếu nại (Notice of Loss / Notice of Claim):**
     - Gửi văn bản Notice of Loss chính thức tới Hãng tàu và Công ty Bảo hiểm trong thời hạn:
       - **Trong vòng 24 giờ** đối với tổn thất thấy rõ bên ngoài.
       - **Chậm nhất 03 ngày làm việc** đối với tổn thất ẩn tỳ bên trong kiện hàng.
  3. **Mời Đơn vị Giám định Độc lập (Independent Marine Surveyor):**
     - Đối với các tổn thất có giá trị ước tính vượt quá **50.000.000 VNĐ (hoặc 2.000 USD)**, bắt buộc phải thuê công ty giám định độc lập (Vinacontrol, SGS, Intertek...) tiến hành khám nghiệm xác định nguyên nhân và tỷ lệ tổn thất.
  4. **Thương lượng & Quyết toán Bồi thường:**
     - Hồ sơ đòi bồi thường gồm: HBL, Commercial Invoice, Packing List, Biên bản COR, Chứng thư giám định (Survey Report), Bảng kê thiệt hại thực tế và Hóa đơn sửa chữa/khắc phục.
     - Mức bồi thường tối đa tuân theo Giới hạn trách nhiệm của Forwarder (Standard Trading Conditions) trừ khi khách hàng đã kê khai giá trị đặc biệt trên vận đơn và nộp phụ phí tương ứng.

---

# PHẦN 4: CHÍNH SÁCH TÀI CHÍNH & BIỂU PHÍ DOANH NGHIỆP (COMMERCIAL POLICIES)

## Chính sách 1: POL-FIN-05: Chính sách Hạn mức Tín dụng & Công nợ Khách hàng (Credit Terms Policy)
- **Phân loại hạn mức tín dụng (Credit Tier):**
  - **Tier 1 (Khách hàng VIP / Hợp đồng năm):** Hạn mức công nợ tối đa 500.000.000 VNĐ, thời hạn thanh toán **30 ngày** kể từ ngày phát hành Hóa đơn VAT.
  - **Tier 2 (Khách hàng Doanh nghiệp thường xuyên):** Hạn mức công nợ tối đa 150.000.000 VNĐ, thời hạn thanh toán **15 ngày**.
  - **Tier 3 (Khách hàng Mới / Khách hàng Vãng lai):** Thanh toán 100% trước khi giao Lệnh giao hàng (D/O) hoặc trước khi phát hành Original B/L.
- **Quy tắc chặn rủi ro (Credit Lock Rules):**
  - Hệ thống tự động khóa tính năng tạo Booking mới và giữ lệnh D/O đối với các tài khoản khách hàng có hóa đơn quá hạn thanh toán trên **07 ngày làm việc**.

## Chính sách 2: POL-PRC-06: Biểu phí Phụ phí Địa phương (Local Charges) & Quy định Phạt Demurrage / Detention
- **Bảng Biểu phí Local Charges Tiêu chuẩn (Tham chiếu FCL):**
  | Tên phụ phí | Mã phí | Container 20ft (VNĐ) | Container 40ft (VNĐ) | Ghi chú |
  | :--- | :--- | :--- | :--- | :--- |
  | Phí xếp dỡ tại bãi cảng | `THC` | 2.600.000 | 3.900.000 | Terminal Handling Charge |
  | Phí phát hành lệnh giao hàng | `D/O` | 850.000 | 850.000 | Delivery Order Fee (per Set) |
  | Phí vệ sinh vỏ container | `CLEANING` | 350.000 | 600.000 | Áp dụng cho hàng khô thông thường |
  | Phí niêm phong chì | `SEAL` | 200.000 | 200.000 | High Security Bolt Seal |
  | Phí khai báo truyền dữ liệu | `MANIFEST` | 350.000 | 350.000 | Áp dụng cho hàng nhập khẩu |
- **Quy định Thời gian Miễn phí & Biểu phí Phạt Lưu container (Dem/Det):**
  - **Thời gian miễn phí (Free Time) mặc định:**
    - Hàng khô (Dry Container): **07 ngày Demurrage** (lưu bãi cảng) + **07 ngày Detention** (lưu vỏ tại kho riêng).
    - Hàng lạnh (Reefer Container): **03 ngày Demurrage** + **03 ngày Detention** (bao gồm tiền điện lạnh cắm tại bến bãi).
  - **Mức phí phạt vượt thời gian miễn phí (Overdue Fee):**
    - Từ ngày 1 - 5 quá hạn: 450.000 VNĐ/ngày (20ft) và 800.000 VNĐ/ngày (40ft).
    - Từ ngày 6 trở đi: 750.000 VNĐ/ngày (20ft) và 1.300.000 VNĐ/ngày (40ft).
