**HỒ SƠ ĐẶC TẢ DỰ ÁN PHẦN MỀM & KẾ HOẠCH NHÂN SỰ**

**DỰ ÁN CONNEXT**

_Nền tảng Nhắn tin Thời gian thực và Cộng tác Đa phương tiện trên Nền Web  
(Web-based Real-time Communication & Collaboration Platform)_

**THÔNG TIN TỔNG QUAN HỒ SƠ DỰ ÁN**

• Tên thương hiệu: Connext (Sự giao thoa giữa Connect - Kết nối & Next - Thế hệ kế tiếp)

• Sản phẩm tham chiếu: Lấy cảm hứng từ Zalo Web — tối ưu cho phạm vi Đồ án Sinh viên

• Ngôn ngữ & Nền tảng: 100% C# (.NET 8/9, Blazor WebAssembly, ASP.NET Core Web API, SignalR)

• Quy mô nhân sự: 11 Thành viên (4 Nhóm: Quản lý, Thiết kế UI/UX, Phát triển Kỹ thuật, QA/QC)

• Thời gian triển khai dự kiến: 12 Tuần (5 Giai đoạn phát triển theo mô hình Agile/Scrum)

| **Hạng mục Hồ sơ**     | **Thông tin Chi tiết**                                                     |
| ---------------------- | -------------------------------------------------------------------------- |
| **Mã Tài liệu**        | CNX-SRS-PM-2026-V1.1                                                       |
| **Phiên bản**          | 1.1 (Cập nhật Đặc tả Use Case và Business Rules)                           |
| **Người lập kế hoạch** | Project Manager (Trưởng dự án) & Đội ngũ Phân tích Nghiệp vụ (BA)          |
| **Đối tượng áp dụng**  | Toàn bộ 11 thành viên dự án Connext (PM, BA, UI/UX, FE, BE, DevOps, QA/QC) |
| **Trạng thái**         | Chính thức phê duyệt để khởi động Sprint 1                                 |

# MỤC LỤC TỰ ĐỘNG

_Lưu ý: Mục lục dưới đây được tạo bằng trường tự động (Automatic TOC Field) chuẩn của Microsoft Word. Khi chỉnh sửa thêm nội dung, bạn chỉ cần nhấp chuột phải vào mục lục và chọn 'Update Field' (hoặc nhấn F9) để tự động cập nhật số trang._

**1\. TỔNG QUAN DỰ ÁN & ĐỊNH VỊ THƯƠNG HIỆU CONNEXT**
1.1. Ý nghĩa tên gọi & Định vị sản phẩm
1.2. Tầm nhìn, Sứ mệnh & Giá trị cốt lõi
1.3. Đối tượng mục tiêu
**2\. KIẾN TRÚC KỸ THUẬT TOÀN DIỆN (100% C# / .NET ECOSYSTEM)**
2.1. Định hướng công nghệ Full-stack C#
2.2. Chi tiết các tầng công nghệ (Tech Stack)
**3\. PHẠM VI DỰ ÁN & ĐẶC TẢ YÊU CẦU CHỨC NĂNG (SRS)**
3.1. Giới hạn phạm vi tính năng (In-Scope & Out-of-Scope)
3.2. Các tác nhân hệ thống (System Actors)
3.3. Yêu cầu tính năng tổng quan
3.4. Quy tắc nghiệp vụ cốt lõi (Business Rules)
3.5. Đặc tả chi tiết Use Case (FR-01 đến FR-09)
3.6. Bảng truy vết yêu cầu (Traceability Matrix)
**4\. YÊU CẦU PHI CHỨC NĂNG & THIẾT KẾ CƠ SỞ DỮ LIỆU**
4.1. Đặc tả Yêu cầu Phi chức năng (Non-Functional Requirements)
4.2. Kiến trúc Thực thể Dữ liệu Cốt lõi (Core Entities)
4.3. Kịch bản Luồng Nghiệp vụ Tiêu biểu (Core Use Case Flow)
**5\. CƠ CẤU TỔ CHỨC (11 THÀNH VIÊN) & PHÂN CÔNG NHIỆM VỤ**
5.1. Sơ đồ phân bổ 11 nhân sự theo 4 nhóm chuyên môn
5.2. Nhiệm vụ & Sản phẩm bàn giao (Deliverables) từng vị trí
5.3. Ma trận trách nhiệm liên nhóm (RACI Matrix)
**6\. LỘ TRÌNH TRIỂN KHAI DỰ ÁN (12 TUẦN - 5 GIAI ĐOẠN)**

---

# 1. TỔNG QUAN DỰ ÁN & ĐỊNH VỊ THƯƠNG HIỆU CONNEXT

## 1.1. Ý nghĩa tên gọi & Định vị sản phẩm

Connext là tên thương hiệu được ghép từ hai từ tiếng Anh mang ý nghĩa chiến lược: Connect (Kết nối cốt lõi giữa con người với con người) và Next (Thế hệ tiếp theo, hướng tới tương lai công nghệ hiện đại). Tên gọi này thể hiện định hướng xây dựng một cầu nối liên lạc tốc độ cao, trực quan và không ngừng đổi mới.

Về mặt định vị sản phẩm, Connext là nền tảng giao tiếp và cộng tác đa phương tiện hoạt động trực tiếp trên trình duyệt web (Web-based Communication Platform), được lấy cảm hứng trực tiếp từ nền tảng Zalo Web tại Việt Nam. Hệ thống kết hợp hài hòa tính tiện dụng, thân thuộc của một ứng dụng nhắn tin, gọi thoại cá nhân thời gian thực với hiệu suất làm việc linh hoạt, khả năng chia sẻ dữ liệu và bảo mật cao của các công cụ cộng tác nhóm hiện đại.

## 1.2. Tầm nhìn, Sứ mệnh & Giá trị cốt lõi

- **Tầm nhìn (Vision):** Trở thành trung tâm liên lạc tức thì thế hệ mới trên môi trường trình duyệt web, loại bỏ hoàn toàn rào cản cài đặt phần mềm rườm rà, cho phép người dùng truy cập không gian giao tiếp mọi lúc, mọi nơi trên mọi thiết bị máy tính và di động.
- **Sứ mệnh (Mission):** Thu hẹp khoảng cách giữa trò chuyện cá nhân thường nhật và luồng xử lý công việc thực tế của nhóm sinh viên, nhân sự văn phòng; mang lại trải nghiệm gửi nhận tin nhắn mượt mà, truyền tải tệp tin trọn vẹn và tối ưu hóa năng suất tập thể.
- **Tức thời & Gọn nhẹ (Real-time & Lightweight):** Tối ưu hóa hiệu năng xử lý trực tiếp trên trình duyệt thông qua WebSocket/SignalR với độ trễ cực thấp.
- **Liền mạch (Seamless Workflow):** Không làm gián đoạn luồng làm việc giữa việc nhắn tin trao đổi, gọi video thảo luận và giao việc nhanh ngay trong cùng một cửa sổ hội thoại.
- **Toàn vẹn dữ liệu (Data Integrity):** Hỗ trợ truyền tải hình ảnh, video giữ nguyên độ phân giải (tương tự tính năng HD trên Zalo) và phân loại kho lưu trữ khoa học.

## 1.3. Đối tượng mục tiêu

- **Người dùng cá nhân:** Cần một công cụ nhắn tin, gọi thoại/video nhanh chóng, giao diện tiếng Việt thân thiện, không phụ thuộc vào ứng dụng cài đặt nặng nề trên máy tính.
- **Nhóm sinh viên, học tập & dự án nhỏ (SMEs):** Cần không gian trao đổi bài tập, đồ án, gửi tài liệu dung lượng cao, tạo bình chọn nhóm (Poll), ghim thông báo quan trọng và phân công đầu việc (To-do list) rõ ràng.

# 2. KIẾN TRÚC KỸ THUẬT TOÀN DIỆN (100% C# / .NET ECOSYSTEM)

## 2.1. Định hướng công nghệ Full-stack C#

Một trong những điểm đặc trưng và đột phá nhất của dự án Connext là việc xây dựng toàn bộ hệ thống (từ giao diện người dùng Frontend, máy chủ xử lý nghiệp vụ Backend cho đến luồng truyền tải thời gian thực) hoàn toàn bằng ngôn ngữ C# trên nền tảng .NET 8/9 hiện đại của Microsoft. Việc đồng nhất ngôn ngữ C# giúp nhóm 11 sinh viên dễ dàng chia sẻ các lớp dữ liệu dùng chung (Shared DTOs, Models, Validation Rules) giữa Client và Server, giảm thiểu lỗi không đồng bộ kiểu dữ liệu và nâng cao tốc độ phát triển.

## 2.2. Chi tiết các tầng công nghệ (Tech Stack)

| **Tầng Kiến trúc**            | **Công nghệ C# / .NET Sử dụng**                                                  | **Vai trò & Lý do lựa chọn trong dự án Connext**                                                                                                                                                                   |
| ----------------------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Frontend (Client-side)**    | Blazor WebAssembly (WASM) / Blazor Web App (.NET 8/9) + MudBlazor / Tailwind CSS | Viết toàn bộ logic tương tác giao diện đơn trang (SPA) bằng C# thay vì JavaScript thuần. Tích hợp thư viện Component chuẩn hóa giúp thiết kế giao diện 3 cột giống Zalo nhanh chóng và mượt mà.                    |
| **Backend API (Server-side)** | ASP.NET Core Web API (C#) theo kiến trúc Clean Architecture                      | Xử lý toàn bộ nghiệp vụ cốt lõi, xác thực người dùng (JWT + Refresh Token), quản lý danh bạ, nhóm chat, phân quyền và cung cấp RESTful API chuẩn Swagger/OpenAPI.                                                  |
| **Real-time Engine**          | ASP.NET Core SignalR (C# Hubs & Blazor SignalR Client)                           | Trái tim của Connext: Quản lý kết nối WebSocket để đẩy tin nhắn tức thời (<100ms), cập nhật trạng thái Đã gửi/Đã nhận/Đã xem, hiển thị đang nhập (Typing) và làm kênh tín hiệu (Signaling) cho cuộc gọi. |
| **Gọi Thoại & Video**         | WebRTC kết hợp Blazor C# JS Interop & SignalR Signaling                          | Sử dụng SignalR Hub bằng C# để bắt tay trao đổi SDP Offer/Answer và ICE Candidates giữa 2 trình duyệt, sau đó truyền luồng âm thanh/hình ảnh P2P trực tiếp.                                                        |
| **Cơ sở dữ liệu & ORM**       | SQL Server (hoặc PostgreSQL) + Entity Framework Core (Code-First) + LINQ         | Thiết kế cơ sở dữ liệu hoàn toàn từ các Class C# thông qua EF Core Migrations. Tối ưu hóa truy vấn lịch sử tin nhắn phân trang (Cursor-based Pagination) bằng LINQ.                                                |
| **Caching & Lưu trữ File**    | Redis (StackExchange.Redis) + Cloudinary / MinIO Object Storage                  | Redis lưu trữ danh sách ConnectionId đang Online của người dùng; Cloudinary/MinIO đảm nhận lưu trữ ảnh HD, video và tệp đính kèm an toàn ngoài cơ sở dữ liệu.                                                      |
| **Hạ tầng & DevOps**          | Docker, Docker Compose, GitHub Actions CI/CD, Nginx (HTTPS/WSS)                  | Đóng gói toàn bộ hệ sinh thái .NET thành Container đồng nhất, tự động kiểm thử và triển khai lên Cloud kèm chứng chỉ SSL phục vụ WebRTC.                                                                           |

# 3. PHẠM VI DỰ ÁN & ĐẶC TẢ YÊU CẦU CHỨC NĂNG (SRS)

## 3.1. Giới hạn phạm vi tính năng (In-Scope & Out-of-Scope)

| **Phân hệ (Module)**            | **Tính năng triển khai (In-Scope - Sinh viên thực hiện)**                                                                                             | **Tính năng lược bỏ (Out-of-Scope - Ngoài phạm vi)**                                                     |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| **1\. Tài khoản & Hồ sơ**       | Đăng ký/Đăng nhập (Email/SĐT + mã OTP gửi qua Email), quên mật khẩu, cập nhật hồ sơ cá nhân, ảnh đại diện, bật/tắt trạng thái Online.                 | Đăng nhập qua quét mã QR trên ứng dụng di động, nhận diện khuôn mặt sinh trắc học (eKYC).                |
| **2\. Danh bạ & Kết bạn**       | Tìm kiếm người dùng qua SĐT/Email/Tên, gửi/chấp nhận/từ chối lời mời kết bạn, quản lý danh sách bạn bè, chặn người dùng (Block).                      | Đồng bộ danh bạ tự động từ SIM điện thoại, gợi ý kết bạn thông minh bằng thuật toán AI/Graph.            |
| **3\. Nhắn tin thời gian thực** | Chat 1-1 và Chat Nhóm qua SignalR; trạng thái tin nhắn (Đã gửi, Đã nhận, Đã xem); Trả lời (Reply), Thả cảm xúc (Reaction), Thu hồi tin nhắn.          | Mã hóa đầu cuối (E2EE) phức tạp cấp phần cứng, tin nhắn tự hủy theo giây, bộ công cụ tự vẽ Sticker động. |
| **4\. Chia sẻ File & Media**    | Gửi ảnh (hỗ trợ tùy chọn HD), video ngắn, tài liệu (PDF, Word, Excel, ZIP <= 100MB); thanh thông tin bên phải tự động phân loại Ảnh/Video, File, Link. | Truyền tệp dung lượng hàng Gigabyte (GB), kho lưu trữ đám mây cá nhân độc lập (My Cloud / ZCloud).       |
| **5\. Gọi Thoại & Video**       | Gọi Audio/Video 1-1 trực tiếp trên trình duyệt (WebRTC + SignalR), bật/tắt mic/camera, chia sẻ màn hình (Screen Sharing).                             | Gọi video nhóm đông người cùng lúc (đòi hỏi cụm máy chủ Media SFU/MCU chuyên dụng chi phí cao).          |
| **6\. Cộng tác Nhóm**           | Tạo nhóm, phân quyền Trưởng/Phó nhóm, duyệt thành viên, ghim tin nhắn đầu khung chat, tạo bình chọn (Poll), giao việc nhanh (To-do).                  | Hệ sinh thái Zalo Official Account (OA), Zalo Mini Apps, Bảng tin mạng xã hội (Nhật ký / Story).         |

## 3.2. Các tác nhân hệ thống (System Actors)

- **1\. Khách (Guest):** Người dùng chưa đăng nhập.
- **2\. Người dùng thông thường (Standard User):** Người dùng đã xác thực tài khoản.
- **3\. Quản trị viên nhóm (Group Admin / Deputy Admin):** Người sáng lập nhóm hoặc thành viên được ủy quyền.
- **4\. Quản trị viên hệ thống (System Admin):** Nhân sự quản trị toàn bộ nền tảng Connext.

## 3.3. Yêu cầu tính năng tổng quan

- **FR-01 (Đăng ký, Đăng nhập & Bảo mật phiên):** Đăng nhập bằng Email/SĐT kèm xác thực OTP. Cấp phát JWT Access Token & Refresh Token.
- **FR-02 (Quản lý Hồ sơ):** Chỉnh sửa Tên, Tiểu sử, Ngày sinh, Ảnh đại diện, Ảnh bìa, thiết lập trạng thái Online/Offline.
- **FR-03 (Tìm kiếm người dùng):** Hỗ trợ tìm kiếm theo SĐT/Email hoặc Tên.
- **FR-04 (Quản lý bạn bè & Chặn):** Gửi/Nhận lời mời kết bạn; Hủy kết bạn; Chặn (Block).
- **FR-05 (Hội thoại 1-1 tức thời):** Gắn cờ trạng thái Đã gửi/Đã nhận/Đã xem, trạng thái Đang gõ phím.
- **FR-06 (Hội thoại nhóm đa thành viên):** Tạo nhóm, tag tên (`@all`, `@Tên`).
- **FR-07 (Tương tác trên tin nhắn):** Reply, 6 loại thả cảm xúc, Thu hồi tin nhắn (trong 24h).
- **FR-08 (Gửi tệp đính kèm & Ảnh HD):** Tùy chọn chất lượng HD cho ảnh, gửi file tài liệu lớn.
- **FR-09 (Kho lưu trữ thông tin hội thoại 3 ngăn):** Phân loại Media, Files, Links ở Right Sidebar.
- **FR-10 (Cuộc gọi WebRTC):** Audio/Video call, tắt mic/cam, chia sẻ màn hình.
- **FR-11 (Ghim tin nhắn):** Ghim tối đa 5 thông báo quan trọng trong nhóm.
- **FR-12 (Bình chọn - Poll):** Tạo khảo sát nhiều lựa chọn.
- **FR-13 (Giao việc - To-do Task):** Gán công việc kèm Deadline cho thành viên trong nhóm.

## 3.4. Quy tắc nghiệp vụ cốt lõi (Business Rules)

Các quy tắc dưới đây được thiết lập nhằm tối ưu hóa tài nguyên hệ thống và định hình hành vi chuẩn của nền tảng Connext:

- **BR-01 (Giới hạn kích thước tệp đính kèm):** 
  - Hình ảnh tĩnh (JPG, PNG, WebP): Tối đa **15MB/tệp** (Để hỗ trợ tốt tính năng Ảnh HD gốc chụp từ điện thoại). Các ảnh thông thường sẽ được nén xuống < 1MB tại Frontend trước khi upload.
  - Tài liệu văn phòng (PDF, DOCX, XLSX, ZIP) & Video ngắn (MP4): Tối đa **100MB/tệp** (Phục vụ nhu cầu gửi bài tập, đồ án, video quay lỗi phần mềm).
- **BR-02 (Quy mô nhóm trò chuyện):**
  - Số lượng thành viên tối thiểu: **3 người** (bao gồm người tạo).
  - Số lượng thành viên tối đa: **50 người/nhóm**. 
- **BR-03 (Định nghĩa trạng thái "Đã nhận"):** Trạng thái "Đã nhận" (Delivered) được tính khi **máy của người nhận (Client) đã kết nối SignalR và tải thành công gói tin**, không phải khi Server vừa ghi nhận vào Database.
- **BR-04 (Luồng lời mời kết bạn):** Lời chào đính kèm tối đa **150 ký tự**. Nếu bị từ chối, người gửi phải đợi thời gian hồi (Cooldown) là **24 giờ** mới có thể gửi lại lời mời.
- **BR-05 (Giới hạn văn bản & Xác thực OTP):**
  - Độ dài tối đa tin nhắn văn bản: **2000 ký tự**.
  - Mã OTP có hiệu lực **3 phút (180 giây)**. Nhập sai quá **5 lần liên tiếp**, tính năng gửi OTP sẽ khóa trong **30 phút**.
- **BR-06 (Quy tắc chặn - Block):** Người bị chặn không thể tìm kiếm, không gửi được tin nhắn riêng, không gọi điện và không xem được trạng thái Online của người chặn. Người chặn cũng không thể thêm người bị chặn vào nhóm mới. (Ngoại lệ: Vẫn thấy tin nhắn của nhau nếu đang ở chung một nhóm từ trước).

## 3.5. Đặc tả chi tiết Use Case (FR-01 đến FR-09)

**FR-01: Đăng ký & Đăng nhập Hệ thống**
- **Luồng chính:** 
  1. Nhập SĐT/Email và mật khẩu. 
  2. Bấm "Đăng nhập" -> Server kiểm tra -> Cấp phát JWT -> Chuyển hướng vào `UI_Chat_Main`. 
  3. Nếu Đăng ký: Nhập form -> Bấm "Đăng ký" -> Nhập mã OTP -> Xác thực thành công -> Cấp JWT.
- **Luồng lỗi:** Sai mật khẩu -> Báo "Thông tin không chính xác". Sai OTP 5 lần -> Báo "Khóa tài khoản 30 phút".
- **Tiêu chí nghiệm thu (AC):** Input form validate định dạng hợp lệ trước khi gọi API. Phiên đăng nhập lưu ở trình duyệt, F5 không bị mất phiên. Mật khẩu nhập vào phải được che (Mask).

**FR-02: Quản lý Hồ sơ & Quyền riêng tư**
- **Luồng chính:** Vào Cài đặt -> Sửa Tên/Tiểu sử/Avatar -> Bấm Lưu -> Cập nhật Database.
- **Luồng lỗi:** Tải ảnh dung lượng > 15MB hoặc sai định dạng -> Hủy thao tác và báo lỗi.
- **AC:** Avatar mới khi cập nhật phải tự động đồng bộ realtime trên giao diện khung chat của bạn bè đang online.

**FR-03: Tìm kiếm người dùng thông minh**
- **Luồng chính:** Nhập SĐT/Email vào thanh tìm kiếm -> API trả về kết quả khớp chính xác -> Hiển thị Profile.
- **Luồng lỗi:** Không tìm thấy -> Hiển thị trạng thái "Không tìm thấy người dùng".
- **AC:** Tìm qua SĐT/Email yêu cầu khớp tuyệt đối (Exact match); Tìm qua tên hiển thị hỗ trợ khớp tương đối.

**FR-04: Quản lý chu trình kết bạn & Chặn**
- **Luồng chính:** Gửi lời mời -> Đối phương nhận thông báo realtime -> Bấm "Chấp nhận" -> Cả 2 thành bạn bè.
- **Luồng lỗi:** Đã kết bạn hoặc bị chặn -> Nút "Kết bạn" bị Disabled.
- **AC:** Khi Chấp nhận lời mời, Server tự động push sự kiện để UI hai bên mở sẵn khung chat 1-1.

**FR-05: Hội thoại 1-1 tức thời**
- **Luồng chính:** Mở chat -> Gõ phím (hiển thị Typing...) -> Nhấn Enter -> UI hiện *Đang gửi* -> Server báo thành công sang *Đã gửi* -> Client kia nhận đổi sang *Đã nhận*.
- **Luồng lỗi:** Mất mạng khi gửi -> Hiện icon báo lỗi màu đỏ (Retry) cạnh tin nhắn.
- **AC:** Chỉ báo "Đang gõ phím" tự động biến mất sau 3 giây nếu người dùng ngừng thao tác.

**FR-06: Hội thoại nhóm đa thành viên**
- **Luồng chính:** Chọn >= 2 bạn bè -> Đặt tên nhóm -> Tạo nhóm. Gõ `@all` để thông báo cho toàn nhóm.
- **Luồng lỗi:** Gõ `@` tên người không thuộc nhóm -> Không hiển thị thẻ tag để chọn.
- **AC:** Lời gọi `@all` sẽ kích hoạt Notification (Rung chuông/Badge) cho mọi thành viên dù họ đang Mute nhóm.

**FR-07: Tương tác trên từng tin nhắn**
- **Luồng chính:** Hover chuột vào tin nhắn -> Bấm icon thả Emoji -> Ghi nhận. Bấm "Thu hồi" -> Đổi nội dung thành "Tin nhắn đã bị thu hồi".
- **Luồng lỗi:** Qua 24h kể từ lúc gửi -> Nút "Thu hồi" bị ẩn.
- **AC:** Thu hồi tin nhắn phải xóa Content trong DB, các client đang mở khung chat đó phải thấy tin nhắn biến mất realtime.

**FR-08: Gửi tệp đính kèm & Ảnh chất lượng gốc (HD)**
- **Luồng chính:** Kéo thả tệp vào chat -> Tải lên Cloud/MinIO -> Trả về URL -> Đính kèm gửi đi.
- **Luồng lỗi:** File > 100MB -> Cảnh báo vượt quá dung lượng cho phép. Đứt mạng khi upload -> Báo lỗi.
- **AC:** Phải hiển thị thanh tiến trình (Progress bar) khi upload file. Ảnh không chọn HD sẽ tự động nén nhỏ dung lượng tại Frontend.

**FR-09: Kho lưu trữ thông tin hội thoại 3 ngăn**
- **Luồng chính:** Bấm icon "Thông tin hội thoại" -> Lấy API Media chia làm 3 Tab: Ảnh/Video, Tài liệu, Link.
- **AC:** Click vào một ảnh trong tab Ảnh/Video sẽ bật cửa sổ xem ảnh toàn màn hình; Hỗ trợ cuộn xuống tự động phân trang (Load more).

## 3.6. Bảng truy vết yêu cầu (Traceability Matrix)

| Mã Yêu Cầu | Tên Chức Năng | Màn hình Figma (Screen Name) | API Endpoint / SignalR Hub (Backend C#) |
| :--- | :--- | :--- | :--- |
| **FR-01** | Đăng ký & Đăng nhập | `UI_Auth_Login`<br>`UI_Auth_Register`<br>`UI_Auth_OTP` | `POST /api/v1/auth/login`<br>`POST /api/v1/auth/register`<br>`POST /api/v1/auth/verify-otp` |
| **FR-02** | Cập nhật Hồ sơ | `UI_Profile_Edit`<br>`UI_Profile_View` | `PUT /api/v1/users/me`<br>`POST /api/v1/files/upload-avatar` |
| **FR-03** | Tìm kiếm người dùng | `UI_Search_Global`<br>`UI_Contact_List` | `GET /api/v1/users/search?q={keyword}` |
| **FR-04** | Kết bạn & Chặn | `UI_Friend_Request`<br>`UI_Modal_Block` | `POST /api/v1/friends/request`<br>`POST /api/v1/friends/accept`<br>`POST /api/v1/users/block` |
| **FR-05** | Chat 1-1 (Realtime) | `UI_Chat_Direct_Window` | `ChatHub: SendMessage`<br>`ChatHub: TypingIndicator`<br>`ChatHub: MessageSeen` |
| **FR-06** | Tạo nhóm & Mention | `UI_Modal_CreateGroup`<br>`UI_Chat_Group_Window` | `POST /api/v1/groups/create`<br>`ChatHub: SendGroupMessage` |
| **FR-07** | Reaction & Thu hồi | `UI_Chat_Bubble_Action` | `ChatHub: ReactToMessage`<br>`ChatHub: RecallMessage` |
| **FR-08** | Đính kèm Ảnh/File | `UI_Chat_FilePicker`<br>`UI_Modal_UploadStatus` | `POST /api/v1/files/upload`<br>`ChatHub: SendMediaMessage` |
| **FR-09** | Kho lưu trữ 3 ngăn | `UI_Sidebar_Media`<br>`UI_Gallery_Viewer` | `GET /api/v1/chats/{id}/media`<br>`GET /api/v1/chats/{id}/files` |

# 4. YÊU CẦU PHI CHỨC NĂNG & THIẾT KẾ CƠ SỞ DỮ LIỆU

## 4.1. Đặc tả Yêu cầu Phi chức năng (Non-Functional Requirements)

| **Mã NFR**       | **Tiêu chuẩn Chất lượng**       | **Chỉ số Đặc tả Kỹ thuật Cần đạt**                                                                                                                                             |
| ---------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **NFR-PERF-01**  | Độ trễ thời gian thực (Latency) | Thời gian truyền nhận tin nhắn văn bản qua SignalR WebSocket đạt dưới 100ms trong điều kiện mạng ổn định. Thời gian phản hồi REST API trung bình < 300ms.                      |
| **NFR-PERF-02**  | Tải đồng thời (Concurrency)     | Máy chủ ASP.NET Core SignalR xử lý ổn định tối thiểu 500 - 1.000 kết nối đồng thời trong môi trường thử nghiệm đồ án mà không rò rỉ bộ nhớ.                                    |
| **NFR-SEC-01**   | An toàn đường truyền            | 100% lưu lượng truy cập sử dụng giao thức mã hóa HTTPS và WSS (Secure WebSocket) để bảo vệ nội dung chat và cấp quyền truy cập Camera/Mic cho WebRTC.                          |
| **NFR-SEC-02**   | Bảo mật dữ liệu & Xác thực      | Mật khẩu băm bằng BCrypt/PBKDF2; kiểm soát phân quyền chặt chẽ ở tầng API và Hub (người dùng không thuộc hội thoại không thể đọc hoặc lắng nghe sự kiện của hội thoại đó).     |
| **NFR-USAB-01**  | Trải nghiệm & Tương thích       | Giao diện Blazor tương thích hoàn toàn với Chrome, Edge, Firefox, Safari; bố cục 3 cột hiển thị chuẩn trên màn hình Desktop/Laptop và tự thu gọn linh hoạt trên Tablet/Mobile. |
| **NFR-AVAIL-01** | Khả năng tự phục hồi kết nối    | Tích hợp cơ chế `WithAutomaticReconnect()` của SignalR Client giúp tự động kết nối lại và đồng bộ tin nhắn bị lỡ ngay khi mạng người dùng có tín hiệu trở lại.               |

## 4.2. Kiến trúc Thực thể Dữ liệu Cốt lõi (Core Entities - EF Core)

Cơ sở dữ liệu quan hệ trên SQL Server được thiết kế thông qua Entity Framework Core (Code-First) gồm 8 bảng thực thể trọng tâm sau:

| **Tên Bảng (Entity)**       | **Khóa chính / Khóa ngoại**                                     | **Các Trường Dữ liệu Chính & Mô tả Lưu trữ**                                                                                                                         |
| --------------------------- | --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Users**                   | PK: Id (Guid)                                                   | PhoneNumber, Email, PasswordHash, DisplayName, AvatarUrl, CoverUrl, Bio, DateOfBirth, IsOnline, LastActiveAt, CreatedAt.                                             |
| **Friendships**             | PK: Id (Guid)<br>FK: RequesterId, AddresseeId                   | Status (Enum: Pending, Accepted, Declined, Blocked), GreetingMessage, CreatedAt, UpdatedAt.                                                                          |
| **Conversations**           | PK: Id (Guid)<br>FK: CreatedBy, LastMessageId                   | Type (Enum: Direct, Group), Title, GroupAvatarUrl, OnlyAdminCanPost, CreatedAt, UpdatedAt.                                                                           |
| **ConversationMembers**     | PK: Id (Guid)<br>FK: ConversationId, UserId                     | Role (Enum: Member, DeputyAdmin, Admin), Nickname, JoinedAt, LastReadMessageId, IsMuted, IsPinned.                                                                   |
| **Messages**                | PK: Id (Guid)<br>FK: ConversationId, SenderId, ReplyToMessageId | MessageType (Text, Image, File, Video, Poll, CallLog, System), Content, IsRecalled, IsPinned, CreatedAt.                                                             |
| **Attachments**             | PK: Id (Guid)<br>FK: MessageId                                  | FileName, FileExtension, ContentType, FileSize (Bytes), StorageUrl, ThumbnailUrl, IsOriginalQuality (HD).                                                            |
| **MessageReactions**        | PK: Id (Guid)<br>FK: MessageId, UserId                          | ReactionType (Enum: Like, Love, Haha, Wow, Sad, Angry), CreatedAt.                                                                                                   |
| **GroupPolls & GroupTasks** | PK: Id (Guid)<br>FK: ConversationId, CreatorId                  | Lưu trữ thông tin câu hỏi bình chọn, các lựa chọn (PollOptions), phiếu bầu (PollVotes) và danh sách công việc giao cho thành viên (AssigneeId, DueDate, TaskStatus). |

## 4.3. Kịch bản Luồng Nghiệp vụ Tiêu biểu (Core Use Case Flow)

**KỊCH BẢN MẪU: NGƯỜI DÙNG A GỬI TIN NHẮN KÈM ẢNH HD CHO NGƯỜI DÙNG B QUA SIGNALR**

1. Khởi tạo tại Blazor Client A: Người dùng A chọn hội thoại với B, đính kèm tệp ảnh, tích chọn 'Chất lượng gốc (HD)' và nhấn Gửi.
2. Tải tệp bất đồng bộ: Blazor Client A hiển thị khung tin nhắn tạm (Optimistic UI - 'Đang gửi...') và gọi REST API `POST /api/files/upload` để đẩy tệp lên Cloudinary/MinIO, nhận về `StorageUrl`.
3. Gửi sự kiện SignalR: Client A gọi phương thức `SendMessageAsync(MessageDto)` trên `ChatHub` của ASP.NET Core SignalR.
4. Xử lý tại Backend Server: `ChatHub` xác thực JWT của A, lưu bản ghi `Message` và `Attachment` vào SQL Server qua EF Core, đồng thời cập nhật `LastMessageId` cho hội thoại.
5. Phân phối thời gian thực: `ChatHub` tra cứu `ConnectionId` của B trong Redis. Nếu B đang trực tuyến, Server đẩy sự kiện `ReceiveMessage` xuống Blazor Client B. Giao diện của B lập tức hiển thị ảnh HD và tự động phản hồi sự kiện `MarkAsSeen` để đổi trạng thái bên máy A thành 'Đã xem'.

# 5. CƠ CẤU TỔ CHỨC (11 THÀNH VIÊN) & PHÂN CÔNG NHIỆM VỤ

## 5.1. Sơ đồ phân bổ 11 nhân sự theo 4 nhóm chuyên môn

| **Nhóm Chuyên môn**                       | **Vị trí Đảm nhiệm (Role)**    | **Số lượng** | **Trọng tâm Trách nhiệm trong Dự án Connext**                                                                                  |
| ----------------------------------------- | ------------------------------ | ------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| **1\. Nhóm Quản lý (Management)**         | Project Manager (PM - Bạn)     | 01           | Điều phối tổng thể 11 thành viên, lập kế hoạch Sprint, quản lý tiến độ, kiểm soát rủi ro và chủ trì thuyết trình bảo vệ.       |
| **1\. Nhóm Quản lý (Management)**         | Business Analyst (BA)          | 01           | Phân tích nghiệp vụ Zalo Web, viết tài liệu SRS, vẽ sơ đồ UML (Use Case, Sequence, Activity) và nghiệm thu nghiệp vụ.          |
| **2\. Nhóm Thiết kế (Design)**            | UX Designer                    | 01           | Nghiên cứu hành vi người dùng, xây dựng User Flow, Sitemap, vẽ Wireframe khung xương và tạo Prototype tương tác.               |
| **2\. Nhóm Thiết kế (Design)**            | UI Designer                    | 01           | Thiết kế nhận diện thương hiệu Connext, xây dựng Design System và hoàn thiện giao diện chi tiết (High-fi UI) trên Figma.       |
| **3\. Nhóm Kỹ thuật (Engineering)**       | Frontend Developer (Blazor C#) | 02           | Phát triển giao diện Web bằng C# Blazor, ghép nối REST API, xử lý kết nối SignalR Client và giao diện cuộc gọi WebRTC.         |
| **3\. Nhóm Kỹ thuật (Engineering)**       | Backend Developer (.NET C#)    | 02           | Thiết kế Database EF Core, xây dựng RESTful API, lập trình SignalR Hubs, bảo mật JWT và tích hợp lưu trữ tệp.                  |
| **3\. Nhóm Kỹ thuật (Engineering)**       | DevOps Engineer                | 01           | Quản lý Git Flow, đóng gói Docker Compose, thiết lập CI/CD tự động (GitHub Actions) và triển khai Server HTTPS/WSS.            |
| **4\. Nhóm Kiểm thử (Quality Assurance)** | QA/QC Engineer                 | 02           | Lập kế hoạch kiểm thử (Test Plan), viết Test Case, kiểm thử API/UI/Real-time, ghi nhận lỗi (Bug Log) và nghiệm thu chất lượng. |

## 5.2. Nhiệm vụ & Sản phẩm bàn giao (Deliverables) từng vị trí

### A. Nhóm Quản lý Dự án & Phân tích Nghiệp vụ (02 Thành viên)
- **1. Project Manager (PM - 01 người - Bạn):** Chịu trách nhiệm cao nhất. Lập Master Plan 12 tuần, chia Sprint 2 tuần trên Jira/Trello. Tổ chức họp giao ban, tháo gỡ vướng mắc, kiểm soát chặt chẽ phạm vi tính năng. Bàn giao: Project Charter, Gantt/Sprint Backlog, Slide thuyết trình.
- **2. Business Analyst (BA - 01 người):** Khảo sát Zalo Web, viết Đặc tả Yêu cầu Phần mềm (SRS). Xác định Business Rules, vẽ Use Case, Activity & Sequence Diagram. Bàn giao: Tài liệu SRS, Bộ sơ đồ UML, User Stories & Acceptance Criteria.

### B. Nhóm Thiết kế Trải nghiệm & Giao diện - UI/UX (02 Thành viên)
- **3. UX Designer (01 người):** Xây dựng User Flow, Sitemap, Wireframe chuẩn 3 cột trên nền Web. Thiết lập Interactive Prototype trên Figma. Bàn giao: User Flow, Wireframe, Prototype.
- **4. UI Designer (01 người):** Thiết kế Logo, quy chuẩn Brand Color, Typography. Xây dựng Design System (Component Library) và phủ màu hoàn chỉnh (High-fidelity UI) cả Light/Dark Mode. Bàn giao: Brand Guideline & Design System, UI Figma chi tiết.

### C. Nhóm Phát triển Kỹ thuật - Engineering (05 Thành viên)
- **5. Backend Developer 1 (Core Database & SignalR):** Thiết kế Solution C# (.NET 8/9), EF Core, lập trình `ChatHub` và `CallSignalingHub` (SignalR) cho tin nhắn tức thời và WebRTC. Bàn giao: EF Core Migrations, SignalR Hubs.
- **6. Backend Developer 2 (Auth, Group & File):** Xây dựng Auth JWT/OTP. Lập trình REST API cho Hồ sơ, Danh bạ, Quản trị Nhóm, tích hợp Cloudinary/MinIO. Bàn giao: REST API (Swagger), Dịch vụ Upload.
- **7. Frontend Developer 1 (Blazor Chat & WebRTC):** Xây dựng UI Khung chat bằng Blazor WASM. Tích hợp `HubConnection` nhận tin nhắn realtime, xử lý Infinite Scroll, giao diện Video Call (WebRTC). Bàn giao: Module Chat & Video Call.
- **8. Frontend Developer 2 (Blazor Auth & Media):** Xây dựng UI Auth, Hồ sơ, Danh bạ, Hộp thoại tạo nhóm, Poll, To-do và Sidebar Media. Bàn giao: Module UI tương ứng.
- **9. DevOps Engineer (01 người):** Quản trị Git Flow, viết `Dockerfile`/`docker-compose.yml`. Xây dựng CI/CD GitHub Actions, deploy lên Cloud, cấu hình Nginx (HTTPS/WSS). Bàn giao: CI/CD Pipeline, Docker Compose, Server Staging/Production.

### D. Nhóm Kiểm thử & Đảm bảo Chất lượng - QA/QC (02 Thành viên)
- **10. QA/QC Engineer 1 (UI/UX & Chức năng):** Viết Test Plan, Test Cases chức năng, kiểm thử UI/UX Blazor đối chiếu Figma. Bàn giao: Test Plan, Test Cases, Báo cáo lỗi UI.
- **11. QA/QC Engineer 2 (API & Real-time):** Dùng Postman test API. Kiểm thử Real-time/WebRTC đa phiên (Multi-session), giả lập rớt mạng. Ghi log Bug lên Jira, test hồi quy. Bàn giao: Postman Collection, Bug Reports, Biên bản UAT.

## 5.3. Ma trận trách nhiệm liên nhóm (RACI Matrix)

*Chú thích: R (Responsible), A (Accountable), C (Consulted), I (Informed).*

| **Hạng mục Công việc**                   | **PM (Bạn)** | **BA** | **UI/UX (2)** | **BE Devs (2)** | **FE Devs (2)** | **DevOps (1)** | **QA/QC (2)** |
| ---------------------------------------- | ------------ | ------ | ------------- | --------------- | --------------- | -------------- | ------------- |
| **Lập kế hoạch & Quản lý Sprint**        | R / A        | C      | I             | I               | I               | I              | I             |
| **Phân tích nghiệp vụ & Viết SRS**       | A            | R      | C             | C               | C               | I              | C             |
| **Thiết kế Wireframe & UI Figma**        | A            | C      | R             | I               | C               | I              | I             |
| **Thiết kế Database & Viết API/SignalR** | I            | C      | I             | R / A           | C               | C              | I             |
| **Xây dựng Giao diện Blazor C#**         | I            | C      | C             | C               | R / A           | I              | I             |
| **Thiết lập Docker, CI/CD & Server**     | A            | I      | I             | C               | C               | R              | I             |
| **Viết Test Case, Bắt lỗi & Nghiệm thu** | A            | C      | I             | C               | C               | I              | R             |

# 6. LỘ TRÌNH TRIỂN KHAI DỰ ÁN (12 TUẦN - 5 GIAI ĐOẠN)

| **Giai đoạn / Thời gian**                                     | **Mục tiêu Trọng tâm**                                                    | **Phối hợp Hoạt động giữa 11 Thành viên**                                                                                                                                                                                                                                                                                   |
| ------------------------------------------------------------- | ------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Giai đoạn 1<br>(Tuần 1 - Tuần 2)<br>Khởi tạo & Phân tích**  | Chốt phạm vi SRS, kiến trúc hệ thống và khung giao diện cơ bản.           | • PM: Lập Master Plan, thiết lập Jira/Trello.<br>• BA: Hoàn thiện tài liệu SRS và sơ đồ Use Case.<br>• UX: Vẽ User Flow và bộ Wireframe 3 cột.<br>• DevOps & BE: Khởi tạo Git, dựng khung Solution C# (.NET) và Docker.                                                                                                     |
| **Giai đoạn 2<br>(Tuần 3 - Tuần 4)<br>Thiết kế & Nền móng**   | Hoàn thành UI Figma, Database và phân hệ Tài khoản/Danh bạ.               | • UI: Thiết kế Design System và màn hình Figma hoàn chỉnh.<br>• BE (2): Tạo Database EF Core, viết API Đăng ký/Đăng nhập/OTP và Kết bạn.<br>• FE (2): Dựng khung Layout Blazor, làm màn hình Auth & Hồ sơ.<br>• QA (2): Viết bộ Test Case chi tiết dựa trên SRS của BA.                                                     |
| **Giai đoạn 3<br>(Tuần 5 - Tuần 8)<br>Phát triển Core Chat**  | Hoàn thiện Nhắn tin thời gian thực SignalR, Chat Nhóm & Gửi File/Ảnh HD.  | • BE (2): Lập trình `ChatHub` SignalR, API Nhóm chat, tích hợp upload ảnh HD/File.<br>• FE (2): Nối `HubConnection` vào Blazor, hiển thị trạng thái Đã xem, Reply, Reaction và kho lưu trữ 3 ngăn.<br>• DevOps: Đưa bản chạy thử lên Staging Server.<br>• QA (2): Kiểm thử cuốn chiếu API và luồng chat thời gian thực. |
| **Giai đoạn 4<br>(Tuần 9 - Tuần 11)<br>Tiện ích & Gọi Video** | Hoàn thiện Gọi Audio/Video WebRTC, Bình chọn (Poll) và Giao việc (To-do). | • BE & FE: Tích hợp Signaling WebRTC cho cuộc gọi 1-1, chia sẻ màn hình, hoàn thiện tính năng Ghim tin, Bình chọn và Giao việc.<br>• DevOps: Cấu hình SSL (HTTPS/WSS) để chạy WebRTC thực tế.<br>• QA (2): Kiểm thử toàn diện (System & Regression Test), săn lỗi.                                                          |
| **Giai đoạn 5<br>(Tuần 12)<br>Đóng gói & Nghiệm thu**         | Ổn định hệ thống Production, hoàn thiện hồ sơ và tổng duyệt Demo.         | • QA (2): Chốt biên bản nghiệm thu chất lượng (UAT).<br>• DevOps & Dev: Tối ưu hiệu năng và khóa nhánh code Production.<br>• PM, BA & Toàn đội: Hoàn thiện cuốn báo cáo tổng kết và diễn tập kịch bản thuyết trình.                                                                                                         |
---

# PHỤ LỤC A. CẬP NHẬT SO VỚI HIỆN TRẠNG TRIỂN KHAI (24/11/2026)

Phụ lục này bổ sung và đính chính bản SRS gốc mà không sửa nội dung các mục phía trên. Khi có mâu thuẫn, nội dung phụ lục là bản áp dụng. Các giá trị nghiệp vụ đã chốt nằm ở [`acceptance-criteria.md`](acceptance-criteria.md). Những mục ghi "chờ PM xác nhận" là đề xuất.

## A.1. Công nghệ giao diện (chờ PM xác nhận)

Giao diện thực tế là HTML, CSS và JavaScript thuần (thư mục `Connext.Web/`, phục vụ qua Nginx), gọi REST API và SignalR bằng JavaScript Client. Các nội dung "Blazor" ở mục 1, 2, 4 và 5 của bản gốc được hiểu là "giao diện web" tương ứng. Backend vẫn là ASP.NET Core Web API (.NET 10), SignalR, SQL Server, MinIO. Redis có trong Docker Compose nhưng API chưa sử dụng; kết nối SignalR được lưu trong bộ nhớ.

## A.2. Đặc tả bổ sung Use Case FR-10 đến FR-13

**FR-10: Gọi thoại/video 1-1**
- **Luồng chính:** Người gọi bấm biểu tượng gọi trong hội thoại 1-1 -> hệ thống gửi `CallIncoming` cho bên nhận -> bên nhận chấp nhận -> hai bên trao đổi `offer`, `answer` và ICE candidate qua SignalR -> luồng âm thanh/video đi trực tiếp (WebRTC) -> một bên kết thúc, hệ thống ghi lịch sử cuộc gọi vào hội thoại.
- **Luồng lỗi:** Bên nhận từ chối -> người gọi nhận thông báo từ chối. Đang trong cuộc gọi khác -> báo máy bận. Không trả lời sau 30 giây -> cuộc gọi nhỡ. Người bị chặn hoặc không thuộc hội thoại -> không gọi được.
- **AC:** Chạy qua HTTPS/WSS; bật/tắt mic, camera; chia sẻ màn hình. Mức tối thiểu: gọi thoại (audio).

**FR-11: Ghim tin nhắn**
- **Luồng chính:** Thành viên chọn tin -> bấm "Ghim" -> tin hiển thị ở đầu khung chat -> thành viên khác thấy ngay.
- **Luồng lỗi:** Quá giới hạn ghim -> báo lỗi. Người không thuộc hội thoại -> bị từ chối.
- **AC:** Tối đa 5 tin ghim mỗi hội thoại; bỏ ghim được.

**FR-12: Bình chọn (Poll)**
- **Luồng chính:** Thành viên nhóm bấm "Tạo bình chọn" -> nhập câu hỏi và 2 đến 10 lựa chọn -> bình chọn hiển thị trong hội thoại -> mỗi người chọn một phương án -> kết quả cập nhật ngay cho cả nhóm.
- **Luồng lỗi:** Ít hơn 2 lựa chọn hoặc câu hỏi quá 200 ký tự -> không tạo được. Người ngoài nhóm -> không xem, không bỏ phiếu.
- **AC:** Mỗi người một phiếu, được đổi lựa chọn; hiển thị số phiếu và phần trăm.

**FR-13: Giao việc (To-do)**
- **Luồng chính:** Thành viên nhóm bấm "Giao việc" -> nhập tiêu đề, chọn người nhận và hạn -> người nhận thấy việc ngay -> đánh dấu hoàn thành -> trạng thái cập nhật cho cả nhóm.
- **Luồng lỗi:** Người nhận không thuộc nhóm -> từ chối. Tiêu đề rỗng hoặc quá 200 ký tự -> không tạo được.
- **AC:** Danh sách lọc theo Chưa xong và Đã xong; việc quá hạn có cảnh báo.

## A.3. Bảng truy vết theo API và Hub thực tế (thay cho mục 3.6)

Đường dẫn thực tế không có tiền tố `/v1`. Chi tiết tham số và mã lỗi xem `docs/api.md` (#72).

| Mã | API REST | Hàm / sự kiện SignalR (`/hubs/chat`) |
|---|---|---|
| FR-01 | `POST /api/auth/register`, `/login`, `/logout`, `/forgot-password`, `/verify-otp`, `/reset-password` | không |
| FR-02 | `GET`, `PUT /api/users/me`; `PUT /api/users/me/password` | `OnlineChanged` |
| FR-03 | `GET /api/contacts/search` | không |
| FR-04 | `POST /api/contacts/request`, `/respond`; `GET /api/contacts/requests`, `/friends`; `DELETE /api/contacts/friends/{id}`; `POST`, `DELETE /api/contacts/blocks/{id}` | `ConversationCreated` |
| FR-05 | `POST /api/conversations/direct/{friendId}`; `GET /api/conversations/{id}/messages`; `POST /api/conversations/{id}/read` | `SendMessage`, `JoinConversation`, `Typing`, `AcknowledgeDelivered` / `ReceiveMessage`, `MessageDelivered`, `Typing` |
| FR-06 | `POST /api/conversations/groups`; `POST`, `DELETE /api/conversations/{id}/members`; `POST /{id}/leave`; `PUT /{id}`, `/avatar`, `/members/{id}/role` | `MessageMentioned`, `ConversationMemberAdded`, `ConversationMemberRemoved`, `ConversationMemberRoleChanged`, `ConversationRenamed`, `GroupAvatarUpdated` |
| FR-07 | `POST`, `DELETE /api/conversations/{id}/messages/{messageId}/reactions`; `DELETE /api/conversations/{id}/messages/{messageId}` | `ReactionAdded`, `ReactionRemoved`, `MessageRecalled` |
| FR-08 | `POST /api/conversations/{id}/files` | `ReceiveMessage` |
| FR-09 | `GET /api/conversations/{id}/media`; `GET /api/conversations/{id}/files/{fileId}` | không |
| FR-10 | không | `GetIceServers`, `InviteCall`, `AnswerCall`, `SendSignal`, `SendIceCandidate`, `EndCall` / `CallIncoming`, `CallAnswered`, `CallBusy`, `CallNoAnswer`, `CallEnded`, `IceCandidate` |
| FR-11 | `POST`, `DELETE /api/conversations/{id}/messages/{messageId}/pin`; `GET /api/conversations/{id}/pins` | `MessagePinned`, `MessageUnpinned` |
| FR-12 | chưa có (#78) | chưa có |
| FR-13 | chưa có (#79) | chưa có |
