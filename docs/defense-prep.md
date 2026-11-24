# Chuẩn bị bảo vệ (Refs #74)

## 1. Dàn ý slide (12 slide, 10 phút nói + demo)
1. Tên đề tài, nhóm, vai trò
2. Bài toán và đối tượng người dùng
3. Mục tiêu và phạm vi (FR-01..13, mức ưu tiên P1/P2)
4. Quy trình làm việc: Scrum nhẹ, GitHub Flow, issue/PR, review chéo
5. Kiến trúc tổng thể (`docs/diagrams/deployment.png`)
6. Cơ sở dữ liệu (`docs/diagrams/erd.png`)
7. Luồng thời gian thực: SignalR, WebRTC (`seq-message`, `seq-call`)
8. Bảo mật: JWT, phân quyền nhóm, rate limiting, chặn file nguy hiểm
9. CI/CD và triển khai Azure VPS
10. Kiểm thử và chất lượng
11. Demo (xem `docs/demo-script.md`)
12. Hạn chế, bài học, hướng phát triển

## 2. Phân công trình bày (đề xuất, PM chỉnh)
| Phần | Người |
|---|---|
| Slide 1–4 | PM/BA |
| Slide 5–8 | BE1, BE2 |
| Slide 9–10 | DevOps, QA1, QA2 |
| Giao diện trong demo | FE1, FE2 |
| Thao tác demo | QA1 |

Mỗi người phải trả lời được câu hỏi thuộc phần mình.

## 3. Câu hỏi dự kiến
1. **Vì sao chọn SignalR thay vì WebSocket thuần?** Có sẵn nhóm, tự kết nối lại, dự phòng giao thức, tích hợp xác thực JWT.
2. **JWT truyền cho SignalR thế nào?** Qua query `access_token` khi bắt tay kết nối; hạn chế là token xuất hiện trong URL, cần HTTPS.
3. **Vì sao dùng SQL Server mà không dùng NoSQL cho tin nhắn?** Phạm vi bài tập, dữ liệu quan hệ rõ, có chỉ mục theo hội thoại; NoSQL là hướng mở rộng.
4. **Redis có trong compose, dùng làm gì?** Hiện chưa dùng; trạng thái online lưu trong bộ nhớ. Muốn chạy nhiều bản API phải chuyển sang Redis backplane. Nên trả lời thẳng.
5. **Hệ thống mở rộng ra nhiều server thế nào?** Redis backplane cho SignalR, tách MinIO/DB, load balancer có sticky session.
6. **Bảo mật gọi video ra sao?** WebRTC mã hóa DTLS-SRTP; signaling qua Hub có kiểm tra thành viên và danh sách tín hiệu cho phép.
7. **Chống upload file độc hại?** Giới hạn dung lượng và định dạng, lưu object storage, không thực thi.
8. **Mật khẩu lưu thế nào?** Băm bằng ASP.NET Identity hasher (PBKDF2); OTP có hạn và giới hạn lần thử.
9. **Chống spam/brute force?** Rate limiting trên API đăng nhập và OTP.
10. **Nhóm quản lý yêu cầu thế nào?** SRS, tiêu chí nghiệm thu (`docs/acceptance-criteria.md`), bảng truy vết, issue gắn FR.
11. **Đã kiểm thử gì?** Unit test backend, test tích hợp, smoke test `/api/health`, kiểm thử thủ công theo AC.
12. **CI/CD hoạt động ra sao?** PR chạy build và test; đẩy lên `main` hoặc tag `v*` kích hoạt deploy qua SSH, cấu hình bí mật nằm trong GitHub Secrets.
13. **SRS ghi Blazor nhưng làm HTML/JS?** Đã đính chính tại Phụ lục A.1 của SRS; lý do: nhẹ, dễ phối hợp theo nhóm. (PM thống nhất cách trả lời.)
14. **Hạn chế lớn nhất?** Chưa có đẩy thông báo ngoài trình duyệt, chưa mở rộng ngang, gọi nhóm chưa có.
15. **Nếu có thêm thời gian?** Gọi nhóm, E2E mã hóa, Redis backplane, ứng dụng di động/PWA.

## 4. Lịch tập
- 26/11: mỗi người viết slide phần mình.
- 27/11: tập toàn bộ có bấm giờ, quay video dự phòng.
- 28/11: kiểm tra hệ thống 1 giờ trước khi trình bày.
