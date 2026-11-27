
# QA2 - API Smoke Testing trong CI/CD

## 1. Mục tiêu

Tự động kiểm thử các API cơ bản của Connext khi tạo
Pull Request hoặc cập nhật nhánh develop và main.

## 2. Công cụ sử dụng

- Postman CLI 1.95.0 (phiên bản kiểm thử local).
- ASP.NET Core 10.
- SQL Server 2022.
- Redis 7.
- GitHub Actions.
- Docker.

Sử dụng Postman CLI thay Newman để phù hợp với hệ sinh
thái Postman hiện tại của dự án.

## 3. Các file triển khai

- tests/qa2-smoke/qa2-smoke.postman_collection.json
- .github/workflows/qa2-api-testing.yml
- docs/qa2-api-ci-testing.md

## 4. Kết quả kiểm thử localhost

Ngày kiểm thử: 27/11/2026.

| Test case | Nội dung | HTTP | Assertions |
| --- | --- | --- | --- |
| TC_CI_01 | API Health | 200 | 2/2 PASS |
| TC_CI_02 | Contacts không có JWT | 401 | 2/2 PASS |
| TC_CI_03 | Contacts JWT giả | 401 | 2/2 PASS |
| TC_CI_04 | SignalR không có JWT | 401 | 2/2 PASS |
| TC_CI_05 | SignalR JWT giả | 401 | 2/2 PASS |

Tổng kết:

- Requests: 5/5 PASS.
- Assertions: 10/10 PASS.
- Failed: 0.
- Tổng thời gian chạy: 1842 ms.
- Thời gian phản hồi trung bình: 153 ms.
- Thời gian phản hồi thấp nhất: 30 ms.
- Thời gian phản hồi cao nhất: 468 ms.

## 5. Hướng dẫn chạy local

Khởi động Backend tại http://localhost:5232.

Chạy lệnh:

    postman collection run ./tests/qa2-smoke/qa2-smoke.postman_collection.json

## 6. Quy trình GitHub Actions

1. Checkout mã nguồn.
2. Cài đặt .NET 10 và Postman CLI.
3. Build Backend.
4. Khởi động SQL Server và Redis bằng Docker.
5. Khởi động API và kiểm tra Health.
6. Chạy bộ Smoke Test.
7. Xuất báo cáo JUnit XML.
8. Dọn dẹp môi trường kiểm thử.

Workflow được thiết kế để chạy độc lập với CI hiện có.

## 7. Bảo mật và phạm vi

- Không sử dụng tài khoản hoặc JWT thật.
- Sử dụng database kiểm thử riêng Connext_QA2_CI.
- Khóa JWT và mật khẩu SQL trong CI được tạo tạm thời.
- Không thay đổi mã nguồn Backend hoặc Frontend.
- Không phụ thuộc Gmail, OTP, MinIO hoặc Azure.
- Chỉ kiểm thử Health và xác thực JWT cơ bản.
- Không thay thế toàn bộ Postman Collection.
- Health endpoint không kiểm tra đầy đủ các dịch vụ phụ thuộc.

## 8. Trạng thái

- Kiểm thử trên Windows localhost: PASS.
- GitHub Actions: chờ xác nhận kết quả thực tế.

## 9. Issue liên quan

- Issue #32.
- Issue #50 (tự động hóa kiểm thử API).
