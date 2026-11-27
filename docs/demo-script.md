# Kịch bản demo Connext (Refs #51, #74)

Thời lượng đề xuất: 8–10 phút. Người trình bày chính: PM. Người thao tác máy: QA1. Địa chỉ demo: https://connext-app.eastasia.cloudapp.azure.com

QA1 ghi nhận 12/12 ca E2E và hồi quy đạt trên nhánh `qa/qa1-business-ui-testing`; xem [báo cáo kiểm thử](../tests/E2E_Demo_And_Regression_Report.md).

## 0. Chuẩn bị trước 30 phút
- [ ] Mở `/api/health` kiểm tra hệ thống sống.
- [ ] Chuẩn bị 3 tài khoản mẫu từ #43 (An, Bình, Chi) và 2 trình duyệt (một cửa sổ ẩn danh).
- [ ] Có sẵn 1 ảnh gốc lớn, 1 file PDF khoảng 20 MB, 1 file bị chặn (vd `.exe`) để minh họa.
- [ ] Bật micro/camera, cấp quyền trình duyệt, thử 1 cuộc gọi.
- [ ] Mở sẵn bản chạy local (Docker Compose) làm phương án dự phòng.

## 1. Luồng demo
| # | Thời điểm | Thao tác | Chức năng | Người làm |
|---|---|---|---|---|
| 1 | 0:00 | Giới thiệu bài toán và kiến trúc bằng lời (không dùng slide) | — | PM |
| 2 | 1:00 | Đăng ký, đăng nhập An; quên mật khẩu bằng OTP (nhanh) | FR-01 | QA1 |
| 3 | 2:00 | Cập nhật hồ sơ, đổi ảnh đại diện | FR-02 | QA1 |
| 4 | 2:30 | Tìm Bình, gửi lời mời kết bạn; Bình chấp nhận ở cửa sổ ẩn danh | FR-03, FR-04 | QA1 |
| 5 | 3:30 | Nhắn tin hai chiều, đang gõ, đã nhận/đã đọc | FR-05 | QA1 |
| 6 | 4:30 | Tạo nhóm, thêm Chi, @mention, đổi quyền quản trị | FR-06 | QA1 |
| 7 | 5:30 | Thả cảm xúc, thu hồi tin, ghim tin | FR-07, FR-11 | QA1 |
| 8 | 6:30 | Kéo thả ảnh/video gốc và file lớn, mở kho media | FR-08, FR-09 | QA1 |
| 9 | 7:30 | Gọi thoại/video 1-1, tắt/bật mic | FR-10 | QA1 |
| 10 | 8:30 | (Nếu hoàn thành) Tạo Poll, giao việc | FR-12, FR-13 | QA1 |
| 11 | 9:30 | Chốt: CI/CD, kiểm thử, kiến trúc, hướng phát triển | — | PM |

## 2. Phương án dự phòng
| Sự cố | Xử lý |
|---|---|
| Web không tải | Chuyển sang bản local Docker Compose; nếu vẫn lỗi, phát video quay sẵn |
| Gọi thoại không kết nối | Nói rõ phụ thuộc mạng/TURN, chuyển sang demo chat, phát đoạn quay sẵn |
| Upload chậm | Dùng file nhỏ hơn đã chuẩn bị |
| FR-12/13 chưa xong | Bỏ bước 10, nêu là hướng phát triển |
| OTP không có email | Dùng OTP hiển thị ở môi trường demo hoặc bỏ qua bước này |

## 3. Quy tắc
- Chốt phiên bản demo (tag `v0.9-rc2` hoặc `v1.0`) trước 27/11; sau đó không merge thêm.
- Quay video demo đầy đủ trong buổi tập 27/11 làm phương án cuối.
- Không dùng dữ liệu cá nhân thật.
