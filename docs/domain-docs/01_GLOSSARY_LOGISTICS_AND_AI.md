# BẢNG THUẬT NGỮ CHUYÊN NGÀNH LOGISTICS & CÔNG NGHỆ / AI (AURORA PLATFORM)

> **Mã tài liệu:** `DOC-AURORA-GLOSSARY-01`  
> **Phiên bản:** 1.0.0  
> **Áp dụng cho:** Toàn bộ hệ sinh thái Aurora Logistics Platform (Client, Admin, Server Microservices)

---

## 1. Thuật ngữ Chuyên ngành Logistics & Vận tải Quốc tế

### 1.1. Vận đơn & Phương thức Vận chuyển
| Thuật ngữ | Tên đầy đủ | Giải thích nghiệp vụ chi tiết | Ứng dụng trong Aurora |
| :--- | :--- | :--- | :--- |
| **B/L** | Bill of Lading | Vận đơn đường biển do người chuyên chở (Carrier) hoặc đại lý phát hành cho người gửi hàng (Shipper), xác nhận đã nhận hàng để vận chuyển và cam kết giao cho người nhận hợp pháp (Consignee). Đóng vai trò là: biên nhận hàng hóa, bằng chứng hợp đồng vận chuyển, và chứng từ sở hữu hàng hóa. | Service `DocumentOcr`, `ShipmentWorkflow`. OCR trích xuất số B/L, cảng đi/đến, trọng lượng, số container. |
| **HBL** | House Bill of Lading | Vận đơn thứ cấp do công ty Forwarder phát hành cho chủ hàng thực tế (Shipper lẻ). | Được quản lý dưới cấp độ Tenant Shipment. |
| **MBL** | Master Bill of Lading | Vận đơn chủ do hãng tàu thực tế phát hành cho Forwarder hoặc trực tiếp cho Shipper FCL. | Liên kết với lô hàng tổng, phục vụ đối soát cước hãng tàu. |
| **AWB** | Air Waybill | Vận đơn hàng không, là biên nhận nhận hàng và bằng chứng hợp đồng vận chuyển bằng đường hàng không (không phải chứng từ sở hữu có thể chuyển nhượng như B/L). | Service `DocumentOcr`, `ShipmentWorkflow`. |
| **FCL** | Full Container Load | Dịch vụ vận chuyển nguyên container. Chủ hàng thuê trọn gói 1 hoặc nhiều container (20ft, 40ft, 40HC, 45ft, Reefer). | Cấu hình loại tải trọng trong `RoutePlanningAgent`. |
| **LCL** | Less than Container Load | Hàng gom, hàng lẻ không đủ đóng nguyên một container. Các chủ hàng nhỏ chia sẻ không gian chung của một container qua dịch vụ đóng ghép (Consolidation) tại kho CFS. | Xử lý gom đơn đa điểm trong `ShipmentWorkflow` & `RoutePlanningAgent`. |
| **CY** | Container Yard | Bãi container tại cảng biển/cảng cạn (ICD), nơi giao nhận nguyên container FCL giữa hãng tàu và chủ hàng/lái xe. | Điểm mốc định vị Geofence trong `GpsTracking`. |
| **CFS** | Container Freight Station | Kho gom/tách hàng lẻ, nơi đóng hàng LCL vào container hoặc dỡ hàng từ container phân phối cho các chủ hàng nhỏ. | Điểm trung chuyển dừng đỗ (Stop/Depot) trong `RoutePlanningAgent`. |
| **ICD** | Inland Container Depot | Cảng cạn / Cảng nội địa, là đầu mối kết nối vận tải đường bộ/đường sắt với cảng biển, hỗ trợ thông quan và bốc dỡ hàng hóa sâu trong nội địa. | Điểm điều phối trạm trong `RoutePlanningAgent`. |
| **POD** | Proof of Delivery | Bằng chứng giao hàng (phiếu giao hàng có ký nhận, hình ảnh chụp tại hiện trường, chữ ký điện tử e-POD). | Trạng thái chốt chuyển đổi vận đơn `ShipmentWorkflow` -> kích hoạt `BillingSettlement`. |
| **D/O** | Delivery Order | Lệnh giao hàng do hãng tàu hoặc forwarder phát hành cho người nhận hàng để xuất trình tại cảng/kho nhận hàng. | Chứng từ tải lên trong `DocumentOcr` & quản lý hồ sơ thông quan. |
| **VGM** | Verified Gross Mass | Khối lượng toàn bộ gộp đã được xác thực của container đóng hàng theo quy chuẩn SOLAS (gồm trọng lượng vỏ container + toàn bộ hàng hóa bên trong). | Bắt buộc đối với vận tải biển, kiểm tra hợp lệ tại `RegulatoryCompliance`. |
| **TEU** | Twenty-foot Equivalent Unit | Đơn vị tương đương container 20 feet (kích thước tiêu chuẩn 20ft x 8ft x 8.5ft, dung tích ~33 m³, tải trọng hàng hóa ~21-28 tấn). | Đơn vị tính dung tích chuẩn cho thuật toán tải trọng xe/hạm đội. |
| **FEU** | Forty-foot Equivalent Unit | Đơn vị tương đương container 40 feet (dung tích ~67 m³). | Đơn vị tính dung tích chuẩn. |
| **Consignor / Shipper** | Người gửi hàng | Cá nhân hoặc tổ chức xuất hàng, đứng tên giao dịch hợp đồng vận chuyển. | Đối tượng đối tác trong `IamTenant` & `CustomerAssistant`. |
| **Consignee** | Người nhận hàng | Người được giao hàng hợp pháp theo quy định ghi trên vận đơn hoặc lệnh giao hàng. | Định danh người thụ hưởng đơn hàng. |
| **Forwarder / NVOCC** | Non-Vessel Operating Common Carrier | Đơn vị giao nhận vận tải không sở hữu tàu, cung cấp dịch vụ logistics trọn gói, gom hàng, thuê chỗ hãng tàu và làm thủ tục hải quan. | Đối tượng Tenant chính sử dụng hệ sinh thái Aurora. |

---

### 1.2. Incoterms 2020 & Chi phí Phụ phí Logistics
| Thuật ngữ | Ý nghĩa & Phân định trách nhiệm | Ứng dụng trong Aurora |
| :--- | :--- | :--- |
| **EXW** (Ex Works) | Giao tại xưởng. Người bán chỉ cần chuẩn bị hàng tại kho/xưởng của mình. Người mua chịu mọi rủi ro và chi phí từ khâu bốc hàng, vận chuyển nội địa, thông quan xuất/nhập khẩu và cước chính. | Xác định phạm vi cung cấp dịch vụ trọn gói của Aurora từ điểm gốc (Origin). |
| **FOB** (Free On Board) | Giao hàng trên tàu. Người bán hoàn tất nghĩa vụ khi hàng đã được xếp an toàn lên tàu tại cảng bốc quy định. Người mua chịu cước tàu chính và mọi chi phí sau đó. | Phổ biến cho các lô hàng xuất khẩu theo đường biển. |
| **CIF** (Cost, Insurance & Freight) | Tiền hàng, bảo hiểm và cước phí. Người bán trả cước vận tải biển đến cảng đích và mua bảo hiểm hàng hải cho lô hàng. Người mua chịu chi phí dỡ hàng và thông quan nhập khẩu. | Tích hợp tính phí bảo hiểm hàng hải trong `FinancialTax` & `BillingSettlement`. |
| **DDP** (Delivered Duty Paid) | Giao đã nộp thuế. Người bán chịu mức trách nhiệm cao nhất: chịu toàn bộ chi phí vận chuyển, rủi ro, thuế nhập khẩu và giao tận kho người mua. | Luồng vận tải nội địa trọn gói trong `ShipmentWorkflow`. |
| **THC** | Terminal Handling Charge | Phụ phí xếp dỡ tại cảng thu bởi cảng biển thông qua hãng tàu để bù đắp chi phí bốc xếp container từ bãi lên tàu hoặc ngược lại. | Thành phần chi phí trong báo giá `NegotiationAgent` và hóa đơn `BillingSettlement`. |
| **Demurrage (DEM)** | Phí lưu bãi / Phí phạt trễ hạn rút container | Phí hãng tàu phạt chủ hàng khi lưu container đầy hàng tại bãi cảng (CY) vượt quá thời gian miễn phí (Free time) cho phép. | Cảnh báo quá hạn tự động theo thời gian thực trong `GpsTracking` & `ShipmentWorkflow`. |
| **Detention (DET)** | Phí lưu vỏ container | Phí phạt khi chủ hàng mượn vỏ container về kho riêng đóng/dỡ hàng vượt quá số ngày miễn phí cho phép trước khi trả lại vỏ rỗng về bãi. | Cảnh báo thời hạn hoàn trả vỏ trong `ShipmentWorkflow`. |
| **Storage Charge** | Phí lưu kho cảng | Phí do ban quản lý cảng thu trực tiếp từ chủ hàng khi container/hàng hóa chiếm chỗ lưu bãi cảng quá thời hạn quy định. | Tính toán chi phí phát sinh bổ sung. |
| **CIC / EBS / LSS** | Container Imbalance Charge / Emergency Bunker Surcharge / Low Sulphur Surcharge | Phụ phí mất cân bằng vỏ container / Phụ phí nhiên liệu khẩn cấp / Phụ phí giảm thải lưu huỳnh theo tiêu chuẩn IMO 2020. | Công thức định giá phụ phí linh hoạt trong `FinancialTax`. |

---

### 1.3. Hải quan, Thủ tục Xuất Nhập Khẩu & Pháp lý
| Thuật ngữ | Tên đầy đủ | Giải thích nghiệp vụ | Ứng dụng trong Aurora |
| :--- | :--- | :--- | :--- |
| **HS Code** | Harmonized System Code | Mã số phân loại hàng hóa quốc tế gồm 6 đến 10 chữ số do Tổ chức Hải quan Thế giới (WCO) ban hành, dùng để áp thuế suất xuất nhập khẩu và chính sách mặt hàng. | Service `RegulatoryCompliance` & RAG AI tự động phân tích gợi ý mã HS từ mô tả hàng hóa. |
| **C/O** | Certificate of Origin | Giấy chứng nhận xuất xứ hàng hóa (Form E, Form D, Form EUR.1, Form AK, Form CPTPP...) giúp xác định nguồn gốc để hưởng thuế suất ưu đãi đặc biệt theo các hiệp định FTA. | Kiểm tra tính hợp lệ và đối soát quy tắc xuất xứ trong `RegulatoryCompliance`. |
| **Customs Declaration** | Tờ khai hải quan | Văn bản điện tử (qua hệ thống VNACCS/VCIS) do doanh nghiệp kê khai thông tin chi tiết lô hàng gửi cho cơ quan hải quan để làm thủ tục thông quan. | Trích xuất số tờ khai, luồng tờ khai (Xanh, Vàng, Đỏ) qua `DocumentOcr`. |
| **Green / Yellow / Red Channel** | Phân luồng Hải quan: Xanh / Vàng / Đỏ | - **Luồng Xanh**: Miễn kiểm tra hồ sơ giấy và miễn kiểm tra thực tế hàng hóa, thông quan tự động.<br>- **Luồng Vàng**: Cơ quan hải quan kiểm tra chi tiết hồ sơ chứng từ điện tử/giấy.<br>- **Luồng Đỏ**: Kiểm tra chi tiết hồ sơ chứng từ và kiểm tra thực tế hàng hóa (soi chiếu container hoặc dỡ hàng kiểm thủ công). | Đánh giá mức độ rủi ro chậm trễ lô hàng và điều chỉnh kế hoạch giao hàng trong `ShipmentWorkflow`. |
| **SOLAS** | Safety of Life at Sea Convention | Công ước Quốc tế về An toàn Tính mạng Con người trên Biển, bắt buộc khai báo trọng lượng container (VGM) trước khi bốc hàng lên tàu. | Quy tắc kiểm soát an toàn bắt buộc trước khi phê duyệt tuyến đường. |
| **Incoterms** | International Commercial Terms | Bộ quy tắc quốc tế do Phòng Thương mại Quốc tế (ICC) ban hành để giải thích các điều kiện thương mại giao nhận hàng hóa. | Chuẩn hóa điều khoản hợp đồng và trách nhiệm chi phí. |

---

## 2. Thuật ngữ Công nghệ & Trí tuệ Nhân tạo (AI & Platform Architecture)

### 2.1. Nền tảng AI, OCR & Máy học trong Aurora
| Thuật ngữ | Tên đầy đủ | Giải thích kỹ thuật | Ứng dụng trong Aurora |
| :--- | :--- | :--- | :--- |
| **OCR** | Optical Character Recognition | Công nghệ nhận dạng ký tự quang học, chuyển đổi hình ảnh hoặc tệp PDF của các chứng từ scan (B/L, Invoice, Packing List) thành văn bản có cấu trúc JSON. | Service `DocumentOcr` sử dụng pipeline xử lý kết hợp layout analysis & LLM Parser. |
| **RAG** | Retrieval-Augmented Generation | Kỹ thuật kết hợp mô hình ngôn ngữ lớn (LLM) với cơ sở tri thức bên ngoài (Vector Database) để truy xuất các văn bản pháp luật, biểu thuế quan, luật hàng hải và trả về câu trả lời chính xác, tránh hiện tượng ảo giác (hallucination). | Service `RegulatoryCompliance` tra cứu văn bản pháp luật hải quan, luật hàng hải quốc tế. |
| **Vector Embeddings** | Nhúng véc-tơ | Biểu diễn văn bản dưới dạng các véc-tơ số học nhiều chiều (ví dụ: 1536 chiều) để tính toán độ tương đồng ngữ nghĩa (Semantic Search) qua khoảng cách Cosine. | Tra cứu nhanh điều luật hải quan và phân loại hàng hóa tương tự. |
| **LLM Agent** | Đại lý Mô hình Ngôn ngữ Lớn | Thực thể AI tự hành có khả năng phân tích ngữ cảnh, suy luận nhiều bước, gọi công cụ (Tool Calling / Function Calling) và đưa ra hành động cụ thể để hoàn thành mục tiêu. | `NegotiationAgent` (đàm phán giá cước), `CustomerAssistant` (giải đáp thắc mắc khách hàng), `DevopsAgent`. |
| **Guardrails / AI Governance** | Hàng rào An toàn & Kiểm soát AI | Cơ chế giám sát, kiểm duyệt đầu vào/đầu ra của AI, ngăn chặn rò rỉ dữ liệu người thuê, kiểm tra định dạng dữ liệu và chặn các hành vi vượt quá thẩm quyền. | Service `ai-governance` (Java Spring Boot) kiểm duyệt mọi tương tác AI. |
| **Human-in-the-Loop (HITL)** | Con người tham gia kiểm duyệt | Cơ chế bắt buộc các quyết định AI có độ tin cậy thấp hoặc rủi ro tài chính/pháp lý cao (Risk Score > Ngưỡng) phải tạm dừng và yêu cầu nhân sự có thẩm quyền phê duyệt. | Quyết định phê duyệt tuyến đường rủi ro cao (`route_planning:approve`) và xác nhận chứng từ OCR sai lệch. |

---

### 2.2. Kiến trúc Hệ thống & Nền tảng Đa người thuê (System & Multi-Tenancy)
| Thuật ngữ | Khái niệm & Cơ chế | Ý nghĩa trong Aurora |
| :--- | :--- | :--- |
| **Multi-Tenancy** | Kiến trúc đa người thuê. Một phiên bản ứng dụng duy nhất phục vụ nhiều doanh nghiệp logistics độc lập (Tenants), đảm bảo cô lập dữ liệu tuyệt đối theo `TenantId`. | Dữ liệu từng công ty khách hàng được mã hóa, phân vùng logic ở cấp Database & BFF. |
| **CBAC** | Capability-Based Access Control | Mô hình kiểm soát truy cập dựa trên năng lực trực tiếp. Quyền hạn nghiệp vụ chi tiết được gắn với mã Permission (ví dụ: `shipment:create`, `route_planning:approve`), tách biệt hoàn toàn với tên gọi của vai trò (Role). | Bảo đảm an toàn vận hành, linh hoạt phân quyền cho nhân sự. |
| **Transactional Outbox** | Mẫu thiết kế lưu trữ sự kiện cùng transaction với dữ liệu nghiệp vụ vào bảng Outbox, sau đó background worker đọc và xuất bản lên RabbitMQ nhằm đảm bảo tính toàn vẹn (At-least-once delivery). | Triển khai trên toàn bộ các service .NET, Java, NestJS để đồng bộ trạng thái đơn hàng. |
| **Event-Driven Architecture (EDA)** | Kiến trúc hướng sự kiện. Các vi dịch vụ giao tiếp bất đồng bộ thông qua việc xuất bản (Publish) và đăng ký (Subscribe) các sự kiện trên RabbitMQ message broker. | Giảm thiểu khớp nối (decoupling) giữa Shipment, OCR, Route Planning và Notification. |
| **BFF (Backend-For-Frontend)** | Lớp API trung gian được tùy biến riêng biệt cho từng loại giao diện người dùng (ví dụ: `Staff.Bff` cho không gian vận hành, `Client.Bff` cho khách hàng). | Tối ưu hóa hiệu năng tải trang, tổng hợp dữ liệu từ nhiều vi dịch vụ và xử lý xác thực bảo mật. |
| **VROOM & OSRM** | Vehicle Routing Open-source Optimization Machine & Open Source Routing Machine | Bộ công cụ mã nguồn mở giải bài toán tối ưu hóa định tuyến phương tiện (VRP) có tính toán khoảng cách thực, thời gian di chuyển, cửa sổ thời gian (Time Windows) và tải trọng xe. | Cốt lõi của vi dịch vụ `RoutePlanningAgent` (.NET 10). |
| **Geofencing** | Hàng rào địa lý ảo | Thiết lập ranh giới địa lý xung quanh cảng, kho, bãi đỗ xe hoặc tuyến đường di chuyển. Khi thiết bị GPS của xe đi vào hoặc ra khỏi ranh giới, hệ thống tự động kích hoạt sự kiện. | Tự động ghi nhận giờ đến/đi tại kho và kích hoạt cảnh báo lệch lộ trình trong `GpsTracking`. |

---
*Tài liệu được biên soạn phục vụ công tác chuẩn hóa tài liệu kỹ thuật và vận hành Aurora Platform.*
