# [Test Case] FR-01, FR-02, FR-03: Xác thực, Hồ sơ và Tìm kiếm

## 1. FR-01: Đăng ký, Đăng nhập & Bảo mật phiên (OTP)
**Mục tiêu:** Đảm bảo luồng đăng ký/đăng nhập bằng Email/SĐT và xác thực OTP hoạt động chính xác.

### 1.1. Kiểm thử Giao diện (UI Testing)
- [ ] Giao diện form Đăng nhập/Đăng ký hiển thị đúng tỷ lệ, không bị vỡ layout trên Desktop và Mobile.
- [ ] Các trường nhập liệu (Input) có hiệu ứng focus rõ ràng, nút bấm (Button) đổi màu khi hover.
- [ ] Hiển thị thông báo lỗi màu đỏ ngay dưới ô nhập liệu nếu có lỗi (trống, sai định dạng).

### 1.2. Kiểm thử Nghiệp vụ (Business Testing)
- [ ] **TC_01_01:** Nhập Email/SĐT không hợp lệ (vd: `abc@`, `123`) -> Hệ thống chặn lại và báo lỗi định dạng.
- [ ] **TC_01_02:** Bỏ trống các trường bắt buộc và nhấn Submit -> Báo lỗi yêu cầu điền đầy đủ.
- [ ] **TC_01_03:** Nhập Email/SĐT hợp lệ -> Hệ thống chuyển sang màn hình nhập OTP và gửi mã 6 số về Email.
- [ ] **TC_01_04:** Nhập sai mã OTP -> Hiển thị thông báo "Mã OTP không chính xác".
- [ ] **TC_01_05:** Nhập đúng mã OTP -> Đăng nhập thành công, chuyển hướng vào giao diện Chat chính.
- [ ] **TC_01_06:** Tải lại trang (F5) sau khi đăng nhập -> Vẫn giữ nguyên phiên đăng nhập (nhờ JWT).

---

## 2. FR-02: Quản lý Hồ sơ Cá nhân
**Mục tiêu:** Người dùng cập nhật được thông tin cá nhân, ảnh đại diện, ảnh bìa và cài đặt trạng thái.

### 2.1. Kiểm thử Giao diện (UI Testing)
- [ ] Khung cập nhật thông tin hiển thị gọn gàng, có nút "Hủy" và "Lưu".
- [ ] Ảnh Avatar và Ảnh bìa (Cover) hiển thị đúng khung cắt (crop) sau khi tải lên.

### 2.2. Kiểm thử Nghiệp vụ (Business Testing)
- [ ] **TC_02_01:** Chỉnh sửa Tên hiển thị, Tiểu sử, Ngày sinh, Giới tính và Lưu -> Cập nhật thành công, hiển thị đúng thông tin mới.
- [ ] **TC_02_02:** Tải lên Avatar/Cover định dạng hình ảnh hợp lệ (JPG, PNG) -> Hệ thống preview và lưu thành công.
- [ ] **TC_02_03:** Bật tùy chọn "Ẩn trạng thái hoạt động" -> Tài khoản hiển thị ngoại tuyến với người khác.
- [ ] **TC_02_04:** Bật tùy chọn "Hiện trạng thái hoạt động" -> Tài khoản hiển thị chấm xanh (Online).

---

## 3. FR-03: Tìm kiếm người dùng thông minh
**Mục tiêu:** Tìm kiếm chính xác bạn bè hoặc người dùng khác trên hệ thống.

### 3.1. Kiểm thử Giao diện (UI Testing)
- [ ] Thanh tìm kiếm (Search bar) nằm ở vị trí dễ thao tác, có icon kính lúp.
- [ ] Danh sách kết quả (Dropdown) không bị tràn che mất các phần tử khác của giao diện.

### 3.2. Kiểm thử Nghiệp vụ (Business Testing)
- [ ] **TC_03_01:** Nhập chính xác Số điện thoại hoặc Email của một người dùng đã đăng ký -> Hiển thị đúng 1 kết quả của người đó.
- [ ] **TC_03_02:** Nhập từ khóa gần đúng theo Tên hiển thị (vd: "Tùng") -> Hiển thị danh sách các người dùng có tên chứa từ khóa.
- [ ] **TC_03_03:** Nhập SĐT/Email/Tên không tồn tại trong hệ thống -> Hiển thị thông báo "Không tìm thấy kết quả".
- [ ] **TC_03_04:** Click vào một kết quả tìm kiếm -> Chuyển đến xem hồ sơ (Profile) của người đó.
