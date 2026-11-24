# [QA1] Kiểm thử Tích hợp Luồng Demo & Checklist Hồi quy (Issue #49)

- **Issue:** #49 (`[QA1] Kiểm thử tích hợp luồng demo và checklist hồi quy`)
- **Người thực hiện:** Đặng Thanh Tùng (QA/QC 1)
- **Môi trường kiểm thử:** Máy chủ Demo / Staging (`develop` / `v0.9-rc1`), Chrome, trình duyệt khác và giao diện điện thoại.
- **Trạng thái tổng thể:** Đạt (Passed) — Không phát sinh bug.

---

## 1. Kịch bản Kiểm thử Đầu cuối (E2E Demo Flow)
- [x] **E2E_01 (Đăng ký):** Đăng ký tài khoản mới thành công, kiểm tra hợp lệ dữ liệu đầu vào.
- [x] **E2E_02 (Đăng nhập & Giữ phiên):** Đăng nhập thành công, chuyển vào màn hình chính và giữ phiên khi tải lại trang.
- [x] **E2E_03 (Quên mật khẩu):** Yêu cầu khôi phục/đặt lại mật khẩu thành công và đăng nhập lại được bằng mật khẩu mới.
- [x] **E2E_04 (Tìm & Kết bạn):** Tìm kiếm người dùng, gửi và chấp nhận lời mời kết bạn thành công; hiển thị đúng trong danh bạ.
- [x] **E2E_05 (Chặn người dùng):** Chặn/bỏ chặn người dùng hoạt động đúng nghiệp vụ và trạng thái tương tác.
- [x] **E2E_06 (Chat 1-1):** Gửi và nhận tin nhắn 1-1 theo thời gian thực ổn định, hiển thị đúng nội dung và thời gian.
- [x] **E2E_07 (Tạo nhóm từ 3 người):** Tạo nhóm từ 3 thành viên trở lên và nhắn tin trong nhóm thành công.
- [x] **E2E_08 (Gửi ảnh & File):** Tải và gửi hình ảnh, tệp đính kèm thành công, hiển thị/tải lại bình thường.
- [x] **E2E_09 (Thông báo):** Thông báo tin nhắn mới và bộ đếm trạng thái chưa đọc/đã đọc cập nhật chính xác.

---

## 2. Checklist Hồi quy sau mỗi lần Merge lớn vào `develop`
- [x] **REG_01:** Hạ tầng, API, dịch vụ thời gian thực và lưu trữ hoạt động ổn định sau khi merge vào `develop`.
- [x] **REG_02:** Các luồng xác thực, danh bạ, nhắn tin 1-1, nhóm chat, gửi ảnh/file và thông báo không bị suy giảm tính năng sau cập nhật.
- [x] **REG_03:** Giao diện hiển thị gọn gàng, không vỡ bố cục trên Chrome, trình duyệt khác và chế độ giao diện điện thoại.

---

## 3. Tổng hợp Kết quả Kiểm thử
| Hạng mục | Số ca kiểm thử | Đạt (Passed) | Lỗi còn mở (Open Bugs) |
| :--- | :---: | :---: | :---: |
| Luồng demo đầu cuối (E2E) | 9 | 9 | 0 |
| Checklist hồi quy sau merge | 3 | 3 | 0 |
| **Tổng cộng** | **12** | **12** | **0** |

**Kết luận:** Toàn bộ luồng demo và checklist hồi quy trên máy chủ demo hoạt động ổn định trên Chrome, trình duyệt khác và giao diện điện thoại; không phát sinh lỗi nào cần mở Issue `bug`.

### ✅ Báo cáo hoàn tất Issue #49: Kiểm thử tích hợp luồng demo và checklist hồi quy

Đã hoàn tất kiểm thử toàn bộ luồng demo và checklist hồi quy trên máy chủ demo (trên Chrome, trình duyệt khác và giao diện điện thoại), tài liệu đã được lưu tại `tests/E2E_Demo_And_Regression_Report.md` trên nhánh `qa/qa1-business-ui-testing`.

- **Số ca kiểm thử đạt:** 12 / 12 (100%)
- **Lỗi còn mở (Open Bugs):** 0 (web hoạt động ổn định, không phát sinh lỗi)
- **Môi trường & thiết bị đã kiểm tra:** Máy chủ demo — Chrome, trình duyệt khác, giao diện điện thoại.
