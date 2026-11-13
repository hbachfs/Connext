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

## API danh bạ và hội thoại

Các API dưới đây yêu cầu JWT Bearer:

| Method | Endpoint | Mô tả |
|---|---|---|
| `POST` | `/api/contacts/request` | Gửi lời mời kết bạn theo số điện thoại |
| `GET` | `/api/contacts/requests` | Trả về `received` và `sent` cho các lời mời đang chờ |
| `POST` | `/api/contacts/respond` | Chấp nhận hoặc từ chối lời mời theo `requestId` |
| `DELETE` | `/api/contacts/friends/{userId}` | Hủy kết bạn |
| `POST` / `DELETE` | `/api/contacts/blocks/{userId}` | Chặn hoặc bỏ chặn; chặn sẽ xóa quan hệ bạn bè/lời mời hiện tại |
| `GET` | `/api/conversations` | Danh sách hội thoại, tin nhắn cuối và số tin chưa đọc |
| `POST` | `/api/conversations/groups` | Tạo nhóm; cần ít nhất 3 người tính cả người tạo và các thành viên khác phải là bạn |
| `GET` | `/api/conversations/{id}/messages?pageSize=50` | Lấy tin nhắn; `pageSize` từ 1 đến tối đa 50 |
| `POST` | `/api/conversations/{id}/read` | Cập nhật mốc đã đọc để tính số tin chưa đọc |

Kết nối thời gian thực dùng SignalR tại `/hubs/chat`, xác thực JWT qua
`access_token`. Hub hỗ trợ `JoinConversation`, `SendMessage`, `Typing` và
`AcknowledgeDelivered`; các sự kiện client nhận gồm `ConversationCreated`,
`ReceiveMessage`, `Typing`, `MessageDelivered` và `OnlineChanged`. Loại tin nhắn
được giới hạn trong `Text`, `Image`, `File`; nội dung tối đa 4.000 ký tự.

Nginx reverse proxy mẫu nằm tại `deploy/nginx/default.conf`. Cấu hình chuyển
`/hubs/chat` và các yêu cầu API tới `127.0.0.1:5232` (cổng HTTP trong profile
Development), bật WebSocket upgrade và tắt access log cho Hub để tránh ghi token
`access_token` trong URL. Khi triển khai, đổi upstream `connext_api` sang địa chỉ
và cổng mà Nginx có thể truy cập tới API.

Sau khi cấu hình SQL Server, áp dụng schema mới bằng:
`dotnet ef database update --project src/Connext.Data --startup-project src/Connext.Api`.

### Auth, CORS và file

- Đăng ký chấp nhận email tùy chọn; người dùng có thể cập nhật email tại
  `PUT /api/users/me` trước khi yêu cầu đặt lại mật khẩu.
- `POST /api/auth/forgot-password`, `POST /api/auth/verify-otp`,
  `POST /api/auth/reset-password` dùng OTP 6 số (hết hạn sau 10 phút, tối đa 5
  lần nhập sai, gửi lại sau 60 giây). SMTP cần cấu hình `Smtp:Host`,
  `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:From` và `Smtp:EnableSsl`
  qua User Secrets hoặc biến môi trường.
- `GET/PUT /api/users/me` đọc/cập nhật hồ sơ; `PUT /api/users/me/password` đổi
  mật khẩu có xác minh mật khẩu hiện tại; `POST /api/auth/logout` đặt trạng thái
  offline.
- CORS chỉ cho phép các origin khai báo trong `Cors:AllowedOrigins`, ví dụ
  `Cors__AllowedOrigins__0=https://localhost:5001`; danh sách mặc định rỗng.
- Số điện thoại Việt Nam được chuẩn hóa về E.164 (`+84...`) khi đăng ký, đăng
  nhập và tìm kiếm. Migration chuẩn hóa dữ liệu hiện có và dừng nếu phát hiện
  số trùng sau chuẩn hóa.
- Upload tại `POST /api/conversations/{id}/files`; ảnh tối đa 100 MB, video
  MP4 tối đa 1 GB, tài liệu tối đa 100 MB. Chỉ cho phép JPG/PNG/GIF/WebP,
  MP4/PDF/TXT/DOCX/XLSX/PPTX. MIME được xác định từ phần mở rộng và chữ ký
  nội dung; file được tải xuống dạng attachment qua API có kiểm tra thành viên.
  `GET /api/conversations/{id}/media?kind=Image|File|Link` trả kho media.
- MinIO cần `Minio:Endpoint`, `Minio:AccessKey`, `Minio:SecretKey`,
  `Minio:Bucket` và `Minio:UseSSL` qua User Secrets/biến môi trường. Bucket
  được tạo riêng tư tự động; không tạo public URL.

Ví dụ thiết lập secrets cục bộ (thay bằng thông tin SMTP/MinIO của môi trường):

```powershell
dotnet user-secrets set "Cors:AllowedOrigins:0" "https://localhost:5001" --project src/Connext.Api
dotnet user-secrets set "Minio:AccessKey" "your-minio-user" --project src/Connext.Api
dotnet user-secrets set "Minio:SecretKey" "your-minio-password" --project src/Connext.Api
dotnet user-secrets set "Smtp:Host" "smtp.example.com" --project src/Connext.Api
dotnet user-secrets set "Smtp:From" "noreply@example.com" --project src/Connext.Api
dotnet user-secrets set "Smtp:Username" "your-smtp-user" --project src/Connext.Api
dotnet user-secrets set "Smtp:Password" "your-smtp-password" --project src/Connext.Api
```

## Công nghệ

| Tầng | Công nghệ |
|---|---|
| Frontend | Blazor WebAssembly, MudBlazor |
| Backend | ASP.NET Core Web API (.NET 8/9), Clean Architecture |
| Thời gian thực | ASP.NET Core SignalR |
| Gọi thoại/video | WebRTC + SignalR Signaling |
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
│   ├── Connext.Client/    # Blazor WebAssembly
│   └── Connext.Data/      # EF Core, Migrations
├── tests/             # Unit test, Postman Collection, Test Case
├── docker-compose.yml
└── .github/workflows/ # CI/CD
```

## Chạy dự án

Yêu cầu: Git, Docker Desktop (hoặc Docker Engine + Compose v2), .NET SDK 8.

### Cấu hình cục bộ

Không lưu khóa JWT hoặc chuỗi kết nối trong `appsettings.json`. Trong môi trường
Development, API đọc các giá trị này từ .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=ConnextDb;Trusted_Connection=True;TrustServerCertificate=True;" --project src/Connext.Api

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 64
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
$rng.Dispose()
dotnet user-secrets set "JwtSettings:SecretKey" $jwtKey --project src/Connext.Api
Remove-Variable jwtKey, bytes
```

Thay chuỗi kết nối bằng thông tin phù hợp với SQL Server của bạn. Trên môi trường
khác, cấu hình các biến `ConnectionStrings__DefaultConnection` và
`JwtSettings__SecretKey`; không commit giá trị bí mật vào repository.

```bash
git clone https://github.com/hbachfs/Connext.git
cd Connext
git checkout develop

# Dựng hạ tầng dev: SQL Server, Redis, MinIO
cp .env.example .env        # rồi mở .env đổi mật khẩu
docker compose up -d
docker compose ps           # chờ các service ở trạng thái "healthy"
```

| Dịch vụ | Địa chỉ cục bộ |
|---|---|
| SQL Server | `localhost:1433` (user `sa`, mật khẩu trong `.env`) |
| Redis | `localhost:6379` (mật khẩu trong `.env`) |
| MinIO API / Console | `localhost:9000` / `http://localhost:9001` |

Tắt hạ tầng: `docker compose down` (giữ dữ liệu) hoặc `docker compose down -v` (xóa cả dữ liệu).

Khi `Connext.Api` và `Connext.Web` có Dockerfile, DevOps sẽ thêm chúng vào `docker-compose.yml`.

### CI

Workflow `.github/workflows/ci.yml` chạy trên mọi PR vào `develop` và `main`: build, test .NET 8 và kiểm tra `docker-compose.yml`. PR cần CI xanh trước khi merge.

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
