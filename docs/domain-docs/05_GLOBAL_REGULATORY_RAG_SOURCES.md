# TÀI LIỆU RAG NỀN TẢNG: LUẬT HÀNG HẢI, HẢI QUAN, CẢNG BIỂN & CÔNG ƯỚC QUỐC TẾ (GLOBAL / PLATFORM-LEVEL)

> **Mã tài liệu:** `RAG-SOURCE-GLOBAL-01`  
> **Phạm vi áp dụng (Visibility):** `PLATFORM` (Toàn hệ sinh thái Aurora, tất cả Tenant đều có quyền truy xuất đối soát)  
> **Cơ quan quản lý dữ liệu:** Aurora System Compliance Admin  
> **Mục tiêu RAG:** Cung cấp nguồn tri thức pháp lý chuẩn hóa để AI Agent trích xuất dẫn chứng (Citations), đối soát tính hợp lệ của hợp đồng/vận đơn và tự động cảnh báo rủi ro vi phạm pháp luật.

---

# PHẦN 1: CÔNG ƯỚC & QUY TẮC THƯƠNG MẠI QUỐC TẾ

## Điều 1: Incoterms® 2020 (International Commercial Terms)
- **Cơ quan ban hành:** Phòng Thương mại Quốc tế (ICC - International Chamber of Commerce).
- **Phân loại 11 điều kiện giao hàng:**
  1. **Nhóm E (Xuất phát tại xưởng):**
     - `EXW (Ex Works)`: Người bán giao hàng tại xưởng/kho của mình; Người mua chịu toàn bộ chi phí, thủ tục hải quan xuất khẩu và rủi ro từ điểm nhận hàng.
  2. **Nhóm F (Cước phí vận chuyển chính chưa trả):**
     - `FCA (Free Carrier)`: Người bán giao hàng cho người chuyên chở do người mua chỉ định tại cơ sở người bán hoặc địa điểm thỏa thuận; Người bán làm thủ tục hải quan xuất khẩu.
     - `FAS (Free Alongside Ship)`: Người bán giao hàng dọc mạn tàu tại cảng bốc quy định; Phù hợp cho hàng rời đường biển.
     - `FOB (Free On Board)`: Rủi ro chuyển giao từ người bán sang người mua ngay khi hàng hóa đã được xếp an toàn lên tàu tại cảng bốc quy định.
  3. **Nhóm C (Cước phí vận chuyển chính đã trả):**
     - `CFR (Cost and Freight)`: Người bán trả cước vận chuyển đến cảng dỡ; Rủi ro chuyển giao ngay khi hàng qua lan can tàu tại cảng bốc.
     - `CIF (Cost, Insurance and Freight)`: Tương tự CFR nhưng người bán bắt buộc phải mua bảo hiểm hàng hải tối thiểu (Institute Cargo Clauses C hoặc thỏa thuận).
     - `CPT (Carriage Paid To)`: Người bán trả cước vận tải đến nơi đến chỉ định; Rủi ro chuyển giao khi giao cho người vận chuyển đầu tiên.
     - `CIP (Carriage and Insurance Paid To)`: Người bán mua bảo hiểm hàng hóa mức bảo hiểm toàn diện (Institute Cargo Clauses A).
  4. **Nhóm D (Giao hàng tại nơi đến):**
     - `DAP (Delivered at Place)`: Người bán giao hàng trên phương tiện vận tải sẵn sàng dỡ tại điểm đến; Người mua làm thủ tục nhập khẩu và nộp thuế.
     - `DPU (Delivered at Place Unloaded)`: Người bán chịu trách nhiệm dỡ hàng an toàn xuống khỏi phương tiện vận tải tại nơi đến quy định.
     - `DDP (Delivered Duty Paid)`: Trách nhiệm cao nhất của người bán: Chịu toàn bộ chi phí, cước vận chuyển, thủ tục thông quan nhập khẩu và nộp toàn bộ thuế/lệ phí tại nước nhập khẩu.

## Điều 2: Công ước SOLAS 1974 - Quy định Bắt buộc về VGM (Verified Gross Mass)
- **Cơ quan ban hành:** Tổ chức Hàng hải Quốc tế (IMO - International Maritime Organization).
- **Cơ chế xác thực khối lượng:**
  1. **Phương pháp 1 (Method 1):** Cân toàn bộ container đã đóng hàng và niêm phong kẹp chì (Seal) bằng thiết bị cân đạt chuẩn kiểm định.
  2. **Phương pháp 2 (Method 2):** Cân từng kiện hàng, bao bì, vật liệu chèn lót (Dunnage), pallet và cộng với trọng lượng rỗng (Tare Weight) ghi trên cửa vỏ container.
- **Quy tắc an toàn hàng hải:**
  - Nghiêm cấm mọi hành vi bốc container lên tàu biển nếu không có phiếu xác nhận VGM trước giờ cắt máng (VGM Cut-off Time).
  - Sai số cho phép giữa VGM khai báo và thực tế không được vượt quá **± 5%** (hoặc tối đa ± 500 kg tùy theo quy chuẩn cảng biển địa phương).

## Điều 3: Quy tắc Hague-Visby & Quy tắc Hamburg (Trách nhiệm Vận chuyển Đường biển)
- **Phạm vi:** Điều chỉnh quyền hạn, nghĩa vụ và trách nhiệm pháp lý phát sinh từ Vận đơn đường biển (Bill of Lading - B/L).
- **Nghĩa vụ của Người vận chuyển (Carrier):**
  - Mẫn cán hợp lý để cung cấp tàu có đủ khả năng đi biển trước và khi bắt đầu hành trình.
  - Biên chế thuyền bộ, trang bị và cung ứng thích hợp cho tàu.
  - Tiếp nhận, bốc xếp, bảo quản, vận chuyển và dỡ hàng hóa một cách cẩn thận và thích hợp.
- **Giới hạn trách nhiệm tài chính (Liability Limits):**
  - **Hague-Visby Rules:** Giới hạn bồi thường tối đa là **666.67 SDR** cho mỗi kiện/đơn vị hàng hóa hoặc **2 SDR** cho mỗi kilogram trọng lượng cả bì của hàng hóa bị mất mát hoặc hư hỏng (tùy theo giá trị nào cao hơn).
  - **Hamburg Rules:** Giới hạn bồi thường là **835 SDR/kiện** hoặc **2.5 SDR/kg**.
- **Thời hiệu khiếu nại (Time Bar for Claims):**
  - Thông báo tổn thất thấy rõ: Phải lập văn bản trước hoặc tại thời điểm nhận hàng.
  - Thông báo tổn thất không thấy rõ: Trong vòng **3 ngày liên tục** kể từ ngày giao hàng.
  - Thời hiệu khởi kiện người vận chuyển: Hết hạn trong vòng **01 năm** (Hague-Visby) hoặc **02 năm** (Hamburg Rules) kể từ ngày giao hàng.

## Điều 4: Bộ luật IMDG Code (Quản lý Vận chuyển Hàng Nguy hiểm Đường biển)
- **Phân loại 9 nhóm hàng nguy hiểm (9 Hazard Classes):**
  - Class 1: Chất nổ (Explosives).
  - Class 2: Khí nén, khí hóa lỏng (Gases).
  - Class 3: Chất lỏng dễ cháy (Flammable Liquids).
  - Class 4: Chất rắn dễ cháy, chất tự cháy (Flammable Solids).
  - Class 5: Chất oxy hóa và hợp chất hữu cơ Peroxide (Oxidizing Substances).
  - Class 6: Chất độc hại và chất lây nhiễm (Toxic & Infectious Substances).
  - Class 7: Vật liệu phóng xạ (Radioactive Material).
  - Class 8: Chất ăn mòn (Corrosive Substances).
  - Class 9: Hàng nguy hiểm khác (Miscellaneous Dangerous Substances).
- **Yêu cầu bắt buộc khi tiếp nhận Booking:**
  - Cung cấp bảng chỉ dẫn an toàn hóa chất **MSDS (Material Safety Data Sheet)** còn hiệu lực.
  - Số đăng ký Liên Hợp Quốc **UN Number** và Nhóm đóng gói **Packing Group (I, II, III)**.
  - Tuân thủ nghiêm ngặt Bảng phân cách hàng nguy hiểm (Segregation Table) trên tàu và bãi cảng.

---

# PHẦN 2: PHÁP LUẬT HÀNG HẢI, CẢNG BIỂN & HẢI QUAN VIỆT NAM

## Điều 5: Bộ luật Hàng hải Việt Nam 2015 (Luật số 95/2015/QH13)
- **Quy định về Vận đơn và Hợp đồng Vận chuyển:**
  - Vận đơn (Bill of Lading) là chứng từ xác nhận quyền sở hữu hàng hóa, bằng chứng của hợp đồng vận chuyển đường biển và biên nhận nhận hàng của người vận chuyển.
  - Vận đơn theo lệnh (To Order B/L) được chuyển nhượng bằng hình thức ký hậu (Endorsement).
- **Quy định về Tổn thất chung (General Average):**
  - Tổn thất chung là những hy sinh hoặc chi phí bất thường được thực hiện một cách cố ý và hợp lý vì sự an toàn chung nhằm cứu tàu, hàng hóa khỏi hiểm họa chung trên biển.
  - Chủ tàu và chủ hàng có nghĩa vụ đóng góp vào quỹ tổn thất chung theo tỷ lệ giá trị tài sản cứu được.
  - Người vận chuyển có quyền lưu giữ hàng hóa tại cảng đến cho đến khi chủ hàng nộp tiền ký quỹ hoặc nộp Chứng thư cam kết đóng góp tổn thất chung (Average Bond/Guarantee).

## Điều 6: Quản lý Hoạt động Cảng biển & Luồng Hàng hải (Nghị định 58/2017/NĐ-CP & 156/2020/NĐ-CP)
- **Thủ tục tàu biển đến và rời cảng:**
  - Đại lý/Chủ tàu phải gửi Thông báo tàu đến cảng chậm nhất 24 giờ trước khi tàu dự kiến đến vùng đón trả hoa tiêu.
  - Bắt buộc sử dụng hoa tiêu hàng hải dẫn tàu khi di chuyển trong luồng hàng hải nội địa và vùng nước cảng biển Việt Nam.
- **Biểu giá dịch vụ tại Cảng biển Việt Nam (Thông tư 39/2023/TT-BGTVT):**
  - Khung giá dịch vụ bốc dỡ container (Lift on / Lift off, THC) phân định rõ ràng giữa Cảng container cửa ngõ quốc tế (Lạch Huyện, Cái Mép - Thị Vải) và Cảng biển nhóm thông thường.
  - Quy định thời gian tính phí lưu bãi container (Demurrage) và phí chiếm dụng luồng lạch.

## Điều 7: Luật Hải quan Việt Nam 2014 & Quy trình Thủ tục Thông quan (Nghị định 08/2015/NĐ-CP & Thông tư 38/2015/TT-BTC)
- **Thời hạn làm thủ tục Hải quan:**
  - Đối với hàng hóa nhập khẩu: Nộp tờ khai trước ngày hàng hóa đến cửa khẩu hoặc trong thời hạn **30 ngày** kể từ ngày hàng hóa đến cửa khẩu.
  - Khai báo điện tử thông qua Hệ thống thông quan tự động **VNACCS/VCIS**.
- **Cơ chế Phân luồng Hải quan:**
  1. **Luồng Xanh (Green Channel):** Chấp nhận thông quan tự động dựa trên dữ liệu khai điện tử; hàng hóa được miễn kiểm tra chi tiết hồ sơ và miễn kiểm tra thực tế.
  2. **Luồng Vàng (Yellow Channel):** Kiểm tra chi tiết hồ sơ chứng từ hải quan (Hóa đơn, Vận đơn, Packing List, C/O, Giấy phép). Không kiểm tra thực tế hàng hóa.
  3. **Luồng Đỏ (Red Channel):** Kiểm tra chi tiết hồ sơ kết hợp kiểm tra thực tế hàng hóa (Soi chiếu container bằng máy soi hoặc kiểm tra thủ công rút ruột bốc xếp).
- **Quy tắc Xuất xứ Hàng hóa (Rules of Origin - C/O):**
  - Hàng hóa được hưởng thuế suất ưu đãi đặc biệt theo các hiệp định FTAs (EVFTA, CPTPP, ACFTA...) bắt buộc phải có C/O hợp lệ hoặc chứng từ tự chứng nhận xuất xứ (REX/Origin Declaration) đáp ứng quy tắc chuyển đổi mã số hàng hóa (CTC) hoặc hàm lượng giá trị gia tăng (RVC / VAC).

## Điều 8: Quy định Kiểm soát Biên giới & Kiểm tra Chuyên ngành
- **Kiểm dịch Động thực vật:** Hàng nông sản, gỗ, thực phẩm tươi sống, thức ăn chăn nuôi phải được cấp Giấy chứng nhận Kiểm dịch (Phytosanitary/Veterinary Certificate) trước khi thông quan mở bãi.
- **Kiểm tra Hiệu suất Năng lượng & Hợp quy (CR Mark):** Thiết bị điện tử, máy móc công nghiệp, hóa chất thuộc danh mục quản lý của Bộ Công Thương/Bộ KH&CN phải nộp giấy đăng ký kiểm tra chất lượng nhà nước trước khi xin giải phóng hàng về kho bảo quản.
