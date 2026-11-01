# Connext

> Nền tảng nhắn tin thời gian thực và cộng tác đa phương tiện trên nền web, lấy cảm hứng từ Zalo Web.
> Đồ án nhóm 11 thành viên, xây dựng 100% bằng C# / .NET.

**Connext** = *Connect* (kết nối) + *Next* (thế hệ kế tiếp). Người dùng nhắn tin, gọi thoại/video, gửi file và cộng tác nhóm ngay trên trình duyệt, không cần cài phần mềm.

## Tính năng

Đánh dấu `[x]` khi tính năng đã được merge vào `develop` và QA đã kiểm thử.

**Ưu tiên 1 (bắt buộc có khi bảo vệ)**
- [ ] Đăng ký, đăng nhập, OTP, JWT, hồ sơ cá nhân (FR-01, FR-02)
- [ ] Tìm người dùng, kết bạn, chặn (FR-03, FR-04)
- [ ] Chat 1-1 thời gian thực: Đã gửi/Đã nhận/Đã xem, đang nhập (FR-05)
- [ ] Chat nhóm, @mention (FR-06)
- [ ] Gửi file và ảnh HD, kho Ảnh/File/Link (FR-08, FR-09)

**Ưu tiên 2 (làm nếu kịp tiến độ)**
- [ ] Reply, reaction, thu hồi tin nhắn (FR-07)
- [ ] Gọi thoại/video 1-1 bằng WebRTC, chia sẻ màn hình (FR-10)
- [ ] Ghim tin nhắn, bình chọn, giao việc (FR-11, FR-12, FR-13)

## Công nghệ

| Tầng | Công nghệ |
|---|---|
| Frontend | ASP.NET Core MVC / Razor Pages, HTML/CSS/JavaScript, Bootstrap |
| Backend | ASP.NET Core Web API (.NET 8/9), Clean Architecture |
| Thời gian thực | ASP.NET Core SignalR (Hub phía server, JavaScript Client phía trình duyệt) |
| Gọi thoại/video | WebRTC (API JavaScript của trình duyệt) + SignalR Signaling |
| Cơ sở dữ liệu | SQL Server + Entity Framework Core (Code-First) |
| Cache | Redis |
| Lưu file | Cloudinary hoặc MinIO |
| Hạ tầng | Docker Compose, GitHub Actions, Nginx |

## Cấu trúc nhánh

| Nhánh | Mục đích |
|---|---|
| `main` | Bản ổn định, dùng để demo và bảo vệ |
| `develop` | Nhánh tích hợp chung, mọi PR đều vào đây |
| `docs/pm-management`, `docs/ba-srs` | Tài liệu quản lý và đặc tả |
| `design/ux-wireframe`, `design/ui-design-system` | Thiết kế UX/UI |
| `feature/be1-*`, `feature/be2-*` | Backend |
| `feature/fe1-*`, `feature/fe2-*` | Frontend |
| `devops/infra-cicd` | Docker, CI/CD, triển khai |
| `qa/qa1-*`, `qa/qa2-*` | Kiểm thử |

Quy tắc làm việc chi tiết xem [CONTRIBUTING.md](CONTRIBUTING.md).

## Cấu trúc thư mục dự kiến

```
Connext/
├── docs/              # SRS, sơ đồ UML, báo cáo
├── design/            # Link Figma, ảnh xuất thiết kế
├── src/
│   ├── Connext.Shared/    # DTO, Enum, Validation dùng chung
│   ├── Connext.Api/       # ASP.NET Core Web API + SignalR Hubs
│   ├── Connext.Web/       # ASP.NET Core MVC/Razor giao diện, wwwroot (JS, CSS)
│   └── Connext.Data/      # EF Core, Migrations
├── tests/             # Unit test, Postman Collection, Test Case
├── docker-compose.yml
└── .github/workflows/ # CI/CD
```

## Chạy dự án

> Mục này sẽ được DevOps cập nhật khi Docker Compose hoàn thành (cuối Tuần 1).

```bash
git clone https://github.com/hbachfs/Connext.git
cd Connext
git checkout develop
# Các bước tiếp theo: cập nhật sau
```

## Lộ trình 4 tuần

| Tuần | Trọng tâm |
|---|---|
| 1 | Chốt phạm vi và SRS rút gọn, thiết kế UI các màn hình chính, dựng khung Solution, Docker, CI, Database |
| 2 | Auth, danh bạ, chat 1-1 thời gian thực |
| 3 | Chat nhóm, gửi file/ảnh HD, kho media, bắt đầu các tính năng ưu tiên 2 |
| 4 | Ổn định, kiểm thử hồi quy, triển khai, hoàn thiện báo cáo và tập demo |

## Tài liệu

- Đặc tả SRS: thư mục [`docs/`](docs/)
- Thiết kế Figma: _(dán link)_
- Bảng công việc: _(dán link GitHub Projects)_
- Tài liệu API (Swagger): _(cập nhật khi có server)_

## Thành viên

| Vai trò | Họ tên | Nhánh phụ trách |
|---|---|---|
| Project Manager | _(tên)_ | `docs/pm-management` |
| Business Analyst | _(tên)_ | `docs/ba-srs` |
| UX Designer | _(tên)_ | `design/ux-wireframe` |
| UI Designer | _(tên)_ | `design/ui-design-system` |
| Backend 1 | _(tên)_ | `feature/be1-core-realtime` |
| Backend 2 | _(tên)_ | `feature/be2-auth-contacts-groups-storage` |
| Frontend 1 | _(tên)_ | `feature/fe1-chat-webrtc` |
| Frontend 2 | _(tên)_ | `feature/fe2-auth-contacts-groups-media` |
| DevOps | _(tên)_ | `devops/infra-cicd` |
| QA/QC 1 | _(tên)_ | `qa/qa1-business-ui-testing` |
| QA/QC 2 | _(tên)_ | `qa/qa2-api-realtime-perf` |

## Đóng góp

Đọc [CONTRIBUTING.md](CONTRIBUTING.md) trước khi tạo Pull Request đầu tiên.
