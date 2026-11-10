# Connext

## Chạy backend (Connext.Api)

### Cấu hình bí mật (KHÔNG commit lên git)
Các giá trị bí mật đặt bằng **User Secrets** (máy dev) hoặc **biến môi trường** (server/CI), không để trong `appsettings.json`.

```bash
cd <thư mục chứa Connext.Api.csproj>
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<chuỗi kết nối SQL Server>"
dotnet user-secrets set "Jwt:Key" "<chuỗi ngẫu nhiên >= 32 ký tự>"
dotnet user-secrets set "Jwt:Issuer" "connext"
dotnet user-secrets set "Jwt:Audience" "connext-clients"
dotnet user-secrets set "Jwt:ExpireMinutes" "60"
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:User" "<email gửi>"
dotnet user-secrets set "Smtp:Password" "<App Password>"
dotnet user-secrets set "Smtp:From" "<email gửi>"
dotnet user-secrets set "Smtp:FromName" "Connext"
```
Tạo chuỗi ngẫu nhiên: `openssl rand -base64 48`.
Biến môi trường tương ứng: đổi `:` thành `__` (vd `Jwt__Key`, `Smtp__Password`).
`Jwt:Key` ngắn hơn 32 byte thì app không khởi động.

### Quên mật khẩu (3 bước)
| Endpoint | Body | Kết quả |
|---|---|---|
| `POST /api/auth/forgot-password` | `{ email }` | Luôn 200 cùng một thông báo (không lộ email có tồn tại). Gửi OTP 6 số, hết hạn sau 5 phút |
| `POST /api/auth/verify-otp` | `{ email, otp }` | 200 `data.resetToken` hoặc 400 thông báo chung. Sai 5 lần thì khóa mã |
| `POST /api/auth/reset-password` | `{ resetToken, newPassword, confirmPassword }` | 200 hoặc 400. Token chỉ dùng 1 lần, hết hạn sau 10 phút |

Giới hạn: gửi lại cách nhau 60 giây, tối đa 5 mã/giờ/tài khoản; 10 request/phút/IP cho 3 endpoint này (429 khi vượt).

### Test
```bash
dotnet test Connext.Api.Tests
```
