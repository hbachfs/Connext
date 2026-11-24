# Tiêu chí nghiệm thu và quy tắc nghiệp vụ Connext

Cập nhật 24/11/2026. Tài liệu gốc: [SRS](SRS.md). Dùng cho QA (#49, #50), BE, FE và buổi demo (#51).

## 1. Quyết định phạm vi (#53)

PM giữ **toàn bộ FR-01 đến FR-13**. Thứ tự làm và nghiệm thu:

| Nhóm | FR | Ghi chú |
|---|---|---|
| Ưu tiên 1 | FR-01 đến FR-06, FR-08, FR-09 | Bắt buộc đạt trước khi tập demo 27/11 |
| Ưu tiên 2 | FR-07, FR-10, FR-11, FR-12, FR-13 | Giữ trong phạm vi. FR-12 và FR-13 chưa có code, chỉ cần đạt mức "tối thiểu" ở mục 3 |

Quy tắc: tính năng nào chỉ đạt một phần thì ghi rõ phần thiếu trong báo cáo, không ghi `[x]` trong README.

## 2. Quy tắc nghiệp vụ: SRS so với code hiện tại

Đối chiếu SRS với `develop` (sau PR #64). Cột "Chốt" là **đề xuất**, PM xác nhận khi duyệt PR. Nguyên tắc: SRS là chuẩn, code sửa theo SRS; chỗ nào SRS khó làm trong thời gian còn lại thì sửa SRS.

| Quy tắc | SRS | Code hiện tại | Chốt | Việc cần làm |
|---|---|---|---|---|
| Ảnh tối đa (BR-01) | 15 MB | 100 MB | 15 MB | BE sửa `MaxImageBytes`; FE chặn trước khi tải |
| Tài liệu, video tối đa (BR-01) | 100 MB | tài liệu 100 MB, video 1 GB | 100 MB | BE sửa `MaxVideoBytes`; DevOps đặt `client_max_body_size` prod khoảng 110M |
| Thành viên nhóm (BR-02) | tối thiểu 3, tối đa 50 | tối thiểu 3; chưa thấy giới hạn tối đa | 3 đến 50 | BE thêm kiểm tra tối đa 50 khi tạo nhóm và thêm thành viên |
| Trạng thái "Đã nhận" (BR-03) | khi client nhận gói tin | đúng (`AcknowledgeDelivered`) | giữ | không |
| Lời mời kết bạn (BR-04) | lời chào tối đa 150 ký tự; bị từ chối thì chờ 24 giờ mới gửi lại | không có lời chào; gửi lại được ngay | cooldown 24 giờ bắt buộc; bỏ lời chào | BE thêm cooldown; BA bỏ "lời chào" khỏi SRS |
| Độ dài tin nhắn (BR-05) | 2000 ký tự | 4000 ký tự | 2000 | BE sửa DTO (cột DB giữ nguyên) |
| OTP (BR-05) | hiệu lực 3 phút; sai 5 lần thì khóa gửi OTP 30 phút | hiệu lực 10 phút; sai 5 lần thì mã bị vô hiệu | 3 phút; khóa 30 phút | BE sửa `OtpLifetime`, thêm khóa 30 phút; cập nhật test QA2 (TC_RESET_04, 05) |
| Chặn (BR-06) | không tìm thấy, không nhắn, không gọi, không thấy online | đã có các kiểm tra chặn | giữ | QA kiểm tra lại từng ý |
| Thu hồi tin (FR-07) | trong 24 giờ | 15 phút | 24 giờ | BE sửa hằng số |
| Reaction (FR-07) | 6 loại: Like, Love, Haha, Wow, Sad, Angry | emoji bất kỳ (tối đa 32 ký tự) | 6 loại | BE giới hạn danh sách; FE hiển thị đúng 6 biểu tượng |
| Ghim tin (FR-11) | tối đa 5 | tối đa 50 | 5 | BE sửa hằng số |
| `@all` (FR-06) | gọi toàn nhóm | chỉ mention từng người | làm | BE: `@all` thành mention mọi thành viên; FE gợi ý `@all` |
| Phó nhóm, duyệt thành viên (FR-06) | có | chỉ Member và Admin | không làm | BA ghi vào "Hướng phát triển" của SRS |
| Refresh token (FR-01) | có | không có | không làm | BA sửa SRS: chỉ dùng JWT truy cập |
| OTP khi đăng ký (FR-01) | có | OTP chỉ dùng cho quên mật khẩu | không làm | BA sửa SRS cho khớp |
| Loại tin nhắn (4.2 SRS) | Text, Image, File, Video, Poll, CallLog, System | Text, Image, File, Call | thêm Poll khi làm FR-12 | BE |
| Đường dẫn API (3.6 SRS) | `/api/v1/...`, tên hàm Hub như `TypingIndicator`, `MessageSeen` | `/api/...`, `Typing`, `POST .../read` | theo code | BA cập nhật bảng truy vết từ `docs/api.md` (#72) |
| Công nghệ giao diện (NFR-USAB-01, mục 2) | Blazor | ASP.NET Core MVC/Razor, HTML/JS (README) | theo README | BA sửa SRS |

## 3. Tiêu chí nghiệm thu từng FR

Dạng "Khi... thì...". Mỗi dòng QA kiểm tra được bằng một test case (Postman hoặc thao tác trên giao diện).

### FR-01 Đăng ký, đăng nhập, quên mật khẩu
- Khi nhập đúng SĐT/email và mật khẩu thì nhận JWT và vào trang chat; F5 không mất phiên.
- Khi sai mật khẩu thì báo "Thông tin không chính xác", không cho biết tài khoản có tồn tại hay không.
- Khi đăng nhập sai quá 5 lần trong 1 phút thì API trả `429`.
- Khi quên mật khẩu, nhập đúng OTP trong 3 phút thì đặt lại được mật khẩu; sai 5 lần thì mã bị khóa; token đặt lại đã dùng hoặc giả mạo bị từ chối.
- Khi JWT hết hạn hoặc sai thì API trả `401` và giao diện đưa về trang đăng nhập.

### FR-02 Hồ sơ
- Khi sửa tên, tiểu sử, ảnh đại diện rồi lưu thì dữ liệu cập nhật và còn nguyên sau khi tải lại trang.
- Khi tải ảnh trên 15 MB hoặc sai định dạng thì bị từ chối kèm thông báo lỗi.
- Khi đổi ảnh đại diện thì bạn bè đang online thấy ảnh mới ngay, không cần tải lại trang.
- Không sửa được hồ sơ của người khác.

### FR-03 Tìm kiếm người dùng
- Khi tìm bằng SĐT hoặc email thì chỉ trả kết quả khớp tuyệt đối.
- Khi tìm bằng tên thì cho phép khớp tương đối.
- Khi không có kết quả thì hiển thị "Không tìm thấy người dùng".
- Người đã chặn mình không xuất hiện trong kết quả.

### FR-04 Kết bạn và chặn
- Khi gửi lời mời thì bên nhận thấy lời mời ngay (real-time).
- Khi chấp nhận thì hai người thành bạn và hội thoại 1-1 xuất hiện sẵn ở cả hai phía.
- Khi bị từ chối thì 24 giờ sau mới gửi lại được; đã là bạn hoặc đang bị chặn thì nút "Kết bạn" bị vô hiệu.
- Khi chặn thì người kia không nhắn, không gọi, không thấy trạng thái online của mình; bỏ chặn thì khôi phục.

### FR-05 Chat 1-1
- Khi gửi tin thì hiện "Đã gửi"; khi máy bên kia nhận được thì đổi sang "Đã nhận"; khi bên kia mở hội thoại thì đổi sang "Đã xem".
- Khi đang gõ thì bên kia thấy "Đang nhập..." và biến mất sau 3 giây nếu ngừng gõ.
- Khi mất mạng lúc gửi thì tin hiện biểu tượng lỗi đỏ kèm nút gửi lại.
- Khi mạng trở lại thì SignalR tự kết nối lại và hiển thị tin bị lỡ.
- Tin nhắn dài quá 2000 ký tự bị từ chối.

### FR-06 Chat nhóm
- Khi tạo nhóm với 2 người (tính cả người tạo) thì báo lỗi; từ 3 đến 50 người, toàn bạn bè, thì tạo được.
- Khi quản trị viên thêm, xóa thành viên, đổi tên nhóm thì mọi thành viên thấy thay đổi ngay.
- Khi bị xóa hoặc rời nhóm thì không nhận thêm tin của nhóm đó.
- Khi người tạo rời nhóm thì quyền quản trị chuyển cho người khác; nhóm còn dưới 3 người sau khi rời thì giải tán.
- Khi gõ `@` thì chỉ gợi ý thành viên trong nhóm; `@all` thông báo cho mọi thành viên.

### FR-07 Reply, reaction, thu hồi
- Khi trả lời một tin thì tin mới hiển thị trích dẫn tin gốc; bấm vào nhảy tới tin gốc.
- Khi thả reaction thì chỉ chọn được 1 trong 6 loại; bấm lại thì bỏ; mọi người thấy cập nhật ngay.
- Khi thu hồi trong 24 giờ thì nội dung bị xóa khỏi DB, mọi máy đang mở thấy "Tin nhắn đã bị thu hồi"; quá 24 giờ hoặc không phải người gửi thì không thu hồi được.

### FR-08 Gửi file và ảnh HD
- Khi gửi ảnh trên 15 MB hoặc file trên 100 MB thì báo vượt dung lượng, không tải lên.
- Khi tải lên thì có thanh tiến trình; ảnh không chọn HD được nén nhỏ ở phía giao diện.
- File đổi đuôi (ví dụ `.exe` đổi thành `.png`) hoặc tên chứa `../` bị từ chối.
- Khi đứt mạng lúc tải thì báo lỗi và cho gửi lại.

### FR-09 Kho Ảnh/File/Link
- Khi mở thông tin hội thoại thì có 3 tab: Ảnh/Video, Tài liệu, Link.
- Khi bấm một ảnh thì mở xem toàn màn hình.
- Khi cuộn xuống cuối danh sách thì tải thêm (phân trang).
- Người không thuộc hội thoại tải file bị `403`.

### FR-10 Gọi thoại/video 1-1
- Khi gọi cho bạn trong hội thoại 1-1 thì bên kia đổ chuông và nhận hoặc từ chối được; sau 30 giây không trả lời thì ghi là cuộc gọi nhỡ trong hội thoại.
- Khi đang gọi thì bật/tắt mic, camera và chia sẻ màn hình được.
- Người bị chặn hoặc không thuộc hội thoại không gọi được và không gửi được tín hiệu.
- Chạy qua HTTPS trên server demo. **Mức tối thiểu để tính là đạt:** gọi thoại (audio) 1-1 hoạt động.

### FR-11 Ghim tin
- Khi ghim thì tin hiện ở đầu khung chat; ghim tin thứ 6 báo lỗi; bỏ ghim được.
- Người ngoài nhóm không ghim hoặc xem danh sách ghim được.

### FR-12 Bình chọn (mới)
- Khi thành viên tạo bình chọn (câu hỏi tối đa 200 ký tự, 2 đến 10 lựa chọn) thì nó hiển thị trong hội thoại như một tin nhắn.
- Khi bình chọn thì mỗi người một phiếu, đổi được lựa chọn; kết quả (số phiếu, phần trăm) cập nhật ngay cho mọi người.
- Người ngoài nhóm không xem và không bỏ phiếu được.
- **Mức tối thiểu:** tạo, bỏ phiếu và xem kết quả trong nhóm. Đóng bình chọn là phần thêm.

### FR-13 Giao việc (mới)
- Khi thành viên tạo việc (tiêu đề tối đa 200 ký tự, người nhận là thành viên nhóm, hạn hoàn thành) thì người nhận thấy ngay.
- Khi người nhận hoặc người tạo đánh dấu hoàn thành thì trạng thái cập nhật cho cả nhóm.
- Danh sách việc lọc được theo "Chưa xong" và "Đã xong"; việc quá hạn hiển thị cảnh báo.
- Người ngoài nhóm không xem hoặc sửa được.
- **Mức tối thiểu:** tạo, xem danh sách, đánh dấu hoàn thành.

## 4. Định nghĩa "Xong" cho demo

Một FR được tick `[x]` trong README khi: đã merge vào `develop`, CI xanh, giao diện nối với API thật (không dùng `fake-chat-data.js`), QA1 chạy qua tất cả tiêu chí ở mục 3 trên bản server demo, và không còn lỗi `critical`.
