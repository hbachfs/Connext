# QA2 – Kiểm thử SignalR Realtime và Load Testing

- Dự án: Connext
- Phạm vi: Issue #32 và #50 (phần SignalR)
- Ngày thực nghiệm: 27/11/2026
- Nhánh: `qa/qa2-signalr-testing`
- Môi trường: Local, `http://localhost:5232`, SQL Server LocalDB
- Máy kiểm thử: Intel Core i5-6300U, 2 nhân, RAM 7,88 GB
- Công cụ: Postman, .NET SDK 10.0.403, Microsoft.AspNetCore.SignalR.Client 10.0.15

## 1. Mục tiêu

Kiểm tra xác thực JWT tại bước Negotiate, kết nối WebSocket thực tế, phân quyền hội thoại, gửi/nhận tin nhắn, Typing, xác nhận đã nhận tin nhắn và trạng thái online/offline. Đánh giá khả năng xử lý các mức tải đề xuất trên máy Local.

## 2. Điều kiện và dữ liệu kiểm thử

- Backend chạy tại địa chỉ Local, database đã được khởi tạo.
- A, B2 và C là thành viên cùng một nhóm hội thoại; D không thuộc nhóm.
- A và B2 là bạn bè, không chặn nhau.
- Sử dụng tài khoản kiểm thử và JWT còn hạn; không lưu JWT hoặc mật khẩu vào Git.
- Không cần MinIO cho các trường hợp tin nhắn văn bản trong phạm vi này.
- Không chạy kiểm thử tải trên Azure.

## 3. Kiểm thử chức năng

| ID | Nội dung | Kết quả mong đợi và thực tế | Trạng thái |
|---|---|---|---|
| TC_SIGNALR_01 | JWT hợp lệ | HTTP 200, Negotiate trả thông tin kết nối và hỗ trợ WebSockets | PASS (3/3) |
| TC_SIGNALR_02 | Không có JWT | HTTP 401, Bearer challenge | PASS (2/2) |
| TC_SIGNALR_03 | JWT giả mạo | HTTP 401, Bearer challenge | PASS (2/2) |
| TC_SIGNALR_04 | JWT hết hạn | Cùng JWT Negotiate 200 khi còn hạn; sau hết hạn HTTP 401, Bearer challenge có invalid_token và thông báo expired | PASS (3/3) |
| TC_SIGNALR_05 | Kết nối WebSocket | State: Connected | PASS |
| TC_SIGNALR_06 | Thành viên tham gia hội thoại | A gọi JoinConversation thành công | PASS |
| TC_SIGNALR_07 | Người ngoài tham gia hội thoại | D kết nối thành công nhưng JoinConversation bị từ chối đúng lý do | PASS |
| TC_SIGNALR_08 | Gửi và nhận tin nhắn | B2 nhận ReceiveMessage với đúng nội dung A gửi, ID hợp lệ | PASS |
| TC_SIGNALR_09 | Người ngoài gửi tin nhắn | D bị từ chối; A không nhận nội dung trái phép trong 3 giây | PASS |
| TC_SIGNALR_10 | Typing | B2 nhận true và false, Sender ID đúng với A trong log | PASS |
| TC_SIGNALR_11 | Người ngoài gửi Typing | D bị từ chối; A không nhận sự kiện Typing trong 3 giây | PASS |
| TC_SIGNALR_12 | AcknowledgeDelivered | A nhận MessageDelivered đúng ID tin nhắn và ID B2 | PASS |
| TC_SIGNALR_13 | Disconnect | A chuyển sang Disconnected, B2 nhận OnlineChanged(false) của A | PASS |

Tổng hợp: 13/13 trường hợp đạt theo kết quả chạy riêng lẻ, gồm 4 request Postman (10/10 assertions) và 9 trường hợp C#. Không phải kết quả chạy toàn bộ collection bằng Newman.

TC_SIGNALR_04: lần kiểm thử ban đầu đạt 2/2 assertions (HTTP 401 và Bearer challenge). Sau đó tách biến `expiredToken` khỏi `token`, bổ sung kiểm tra `invalid_token` và lý do `expired` trong header `WWW-Authenticate`. Ngày 27/11/2026, tạm đặt `JwtSettings__ExpiryMinutes=1` trên Backend Local; request đăng nhập tạm thời đạt 2/2 assertions (đăng nhập 200 và Negotiate 200 khi JWT còn hạn). Sau khi chờ hết hạn, TC_SIGNALR_04 đạt 3/3 assertions. Hai assertion của request tạm thời không tính vào 10 assertion của bốn request SignalR. Backend đã khởi động lại thành công sau thao tác khôi phục; log lệnh khôi phục hiển thị thiếu một phần nên cần kiểm tra thời hạn JWT mới để xác nhận cấu hình thực tế. Không sửa appsettings.json.

## 4. Hướng dẫn chạy lại

### 4.1. Backend

Từ thư mục gốc repository, mở Terminal riêng:

```powershell
dotnet run --project .\src\Connext.Api
```

Kiểm tra API lắng nghe tại `http://localhost:5232`.

### 4.2. Postman

Sử dụng environment Local với `baseUrl` và JWT của tài khoản thử nghiệm. Các request nằm trong collection `Connext - QA2 API Testing`.

- TC_SIGNALR_01: Bearer JWT hợp lệ.
- TC_SIGNALR_02: No Auth.
- TC_SIGNALR_03: Bearer `fake.invalid.token`.
- TC_SIGNALR_04: Bearer `{{expiredToken}}`, phải là JWT thực sự đã hết hạn, được lưu riêng trong environment Local. Không dùng JWT còn hạn, token giả hoặc để trống biến.

#### Tái hiện TC_SIGNALR_04

1. Dừng Backend Local, lưu cấu hình thời hạn trước khi thay đổi và đặt thời hạn tạm thời:

```powershell
$qa2PreviousExpiry = $env:JwtSettings__ExpiryMinutes
$env:JwtSettings__ExpiryMinutes = "1"
dotnet run --project .\src\Connext.Api
```

2. Trong Postman, chọn environment Local. Tạo HTTP request đăng nhập tạm thời (`POST {{baseUrl}}/api/Auth/login`, No Auth, Body JSON chứa thông tin tài khoản kiểm thử). Không lưu thông tin đăng nhập vào repository. Dùng script After response sau để lấy JWT riêng và xác minh Negotiate khi còn hạn:

```javascript
pm.test("Dang nhap thanh cong", function () {
    pm.response.to.have.status(200);
});

if (pm.response.code === 200) {
    const data = pm.response.json();
    const jwt = data.token || data.accessToken;
    if (jwt) {
        pm.environment.set("expiredToken", jwt);
        pm.sendRequest({
            url: pm.environment.get("baseUrl") +
                "/hubs/chat/negotiate?negotiateVersion=1",
            method: "POST",
            header: { "Authorization": "Bearer " + jwt }
        }, function (err, res) {
            pm.test("JWT hoat dong khi con han", function () {
                pm.expect(err).to.not.exist;
                pm.expect(res.code).to.eql(200);
            });
        });
    } else {
        pm.test("Response co JWT", function () {
            pm.expect(jwt).to.be.ok;
        });
    }
}
```

3. Send request tạm thời, xác nhận 2/2 PASS. Chờ qua thời điểm hết hạn (khoảng 70 giây với cấu hình 1 phút, ClockSkew bằng 0). Không đăng nhập lại hoặc ghi đè `expiredToken`.
4. Send TC_SIGNALR_04, xác nhận HTTP 401 và 3/3 PASS. Script After response kiểm tra status 401, header `WWW-Authenticate` chứa `Bearer` và `invalid_token`, đồng thời khớp `/expired/i`. Nếu Backend không cung cấp chi tiết trong header, kiểm tra log xác thực thay vì coi mọi 401 là JWT hết hạn.
5. Dừng Backend và khôi phục cấu hình trong đúng Terminal đã đặt biến:

```powershell
if ($null -eq $qa2PreviousExpiry) {
    Remove-Item Env:JwtSettings__ExpiryMinutes -ErrorAction SilentlyContinue
} else {
    $env:JwtSettings__ExpiryMinutes = $qa2PreviousExpiry
}
dotnet run --project .\src\Connext.Api
```

6. Xác minh JWT mới có thời hạn theo cấu hình thông thường (mặc định 1440 phút), xóa riêng request đăng nhập tạm thời nếu đã lưu vào collection. Giữ `expiredToken` ở Local nếu cần chạy lại, không commit giá trị JWT hoặc environment Local.

### 4.3. Biến môi trường cho C#

Mở Terminal PowerShell thứ hai. Có thể dùng hàm sau để đăng nhập và nhận JWT trực tiếp từ API, tránh nhập tay token hoặc lưu bí mật vào code.

```powershell
function Get-QA2Token {
    param([string]$Account)
    $phone = Read-Host "So dien thoai $Account"
    $password = Read-Host "Mat khau $Account" -AsSecureString
    $body = @{
        phoneNumber = $phone
        password = ([PSCredential]::new("QA2", $password)).GetNetworkCredential().Password
    } | ConvertTo-Json

    $response = Invoke-RestMethod `
        -Uri "http://localhost:5232/api/Auth/login" `
        -Method Post -ContentType "application/json" -Body $body -ErrorAction Stop
    $jwt = $response.token
    if (-not $jwt) { $jwt = $response.accessToken }
    if ($jwt -notmatch '^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$') {
        throw "Khong nhan duoc JWT dung dinh dang."
    }
    return $jwt
}

$env:QA2_TOKEN = Get-QA2Token "A"
$env:QA2_TOKEN_B2 = Get-QA2Token "B2"
$env:QA2_TOKEN_D = Get-QA2Token "D"

$env:QA2_CONVERSATION_ID = Read-Host "ID nhom hoi thoai A, B2, C"
$env:QA2_A_USER_ID = Read-Host "User ID cua A"
$env:QA2_B2_USER_ID = Read-Host "User ID cua B2"
```

Nhập ID theo dữ liệu đã tạo trên database Local. Hàm hỗ trợ đăng nhập trên đây được tổng hợp từ quy trình đã sử dụng cho D và B2; chưa chạy thử nguyên hàm này.

Chạy kiểm thử chức năng:

```powershell
dotnet run --project ".\tests\Connext.QA2.SignalR"
```

Kết quả mong đợi: TC_SIGNALR_05 đến TC_SIGNALR_13 đều PASS.

## 5. Kế hoạch kiểm thử tải

Sử dụng project `tests/Connext.QA2.SignalR.LoadTest`. Một kết nối A gửi tin nhắn tuần tự theo lịch, các kết nối B2 nhận sự kiện đồng thời. Tổng lượt nhận dự kiến = số tin nhắn dự kiến × (số kết nối - 1). Không tính lượt nhận của chính A.

| ID | Kết nối | Tin/giây mục tiêu | Thời gian (s) | Tin nhắn | Lượt nhận |
|---|---:|---:|---:|---:|---:|
| TC_LOAD_01 | 5 | 1 | 30 | 30 | 120 |
| TC_LOAD_02 | 10 | 2 | 30 | 60 | 540 |
| TC_LOAD_03 | 20 | 5 | 30 | 150 | 2850 |
| TC_LOAD_04 | 50 | 10 | 60 | 600 | 29400 |
| TC_LOAD_05 | 100 | 20 | 60 | 1200 | 118800 |

Tiêu chí PASS do QA2 đề xuất: đủ kết nối và JoinConversation, duy trì đủ kết nối cuối bài, gửi đủ số tin nhắn kế hoạch và nhận đủ lượt dự kiến. Chưa có ngưỡng latency/SLA chính thức được xác nhận.

Chạy lần lượt, không chạy đồng thời:

```powershell
dotnet build ".\tests\Connext.QA2.SignalR.LoadTest\Connext.QA2.SignalR.LoadTest.csproj"
dotnet run --project ".\tests\Connext.QA2.SignalR.LoadTest" --no-build -- 5 1 30
dotnet run --project ".\tests\Connext.QA2.SignalR.LoadTest" --no-build -- 10 2 30
dotnet run --project ".\tests\Connext.QA2.SignalR.LoadTest" --no-build -- 20 5 30
dotnet run --project ".\tests\Connext.QA2.SignalR.LoadTest" --no-build -- 50 10 60
dotnet run --project ".\tests\Connext.QA2.SignalR.LoadTest" --no-build -- 100 20 60
```

## 6. Kết quả thực nghiệm

Số liệu từ Terminal ngày 27/11/2026; bảng sử dụng lần chạy cuối cùng của từng mức sau sửa lịch gửi.

| Chỉ số | LOAD_01 | LOAD_02 | LOAD_03 | LOAD_04 | LOAD_05 |
|---|---:|---:|---:|---:|---:|
| Kết nối thành công / yêu cầu | 5/5 | 10/10 | 20/20 | 50/50 | 100/100 |
| Kết nối cuối bài | 5 | 10 | 20 | 50 | 100 |
| Khởi tạo (ms) | 525 | 457 | 1733 | 770 | 1138 |
| Thời gian tạo tải (s) | 30.00 | 30.01 | 30.01 | 60.01 | 60.01 |
| Gửi thành công / dự kiến | 30/30 | 60/60 | 150/150 | 600/600 | 1200/1200 |
| Gửi thất bại | 0 | 0 | 0 | 0 | 0 |
| Chưa gửi | 0 | 0 | 0 | 0 | 0 |
| Lượt nhận thực tế / dự kiến | 120/120 | 540/540 | 2850/2850 | 29400/29400 | 118800/118800 |
| Tỷ lệ nhận (%) | 100 | 100 | 100 | 100 | 100 |
| Tỷ lệ lỗi gửi/không đạt (%) | 0 | 0 | 0 | 0 | 0 |
| Throughput (msg/s) | 1.00 | 2.00 | 5.00 | 10.00 | 20.00 |
| Latency Min (ms) | 7.25 | 6.08 | 6.01 | 5.90 | 5.62 |
| Latency Avg (ms) | 14.03 | 16.06 | 22.77 | 18.16 | 13.47 |
| Latency Max (ms) | 58.09 | 123.04 | 166.65 | 465.66 | 146.37 |
| Latency P95 (ms) | 17.41 | 25.87 | 51.94 | 45.31 | 26.60 |
| Kết quả | PASS | PASS | PASS | PASS | PASS |

LOAD_01 được chạy lại để thu đủ chỉ số Latency Avg. Bảng trên sử dụng toàn bộ số liệu của lần chạy cuối cùng (khởi tạo 525 ms, tạo tải 30.00 s), không ghép số liệu từ các lần chạy khác.

Throughput là số lời gọi gửi thành công chia thời gian tạo tải. Latency đo từ lúc bắt đầu gửi đến lúc kết nối nhận sự kiện, trên cùng máy. P95 là mức mà khoảng 95% lượt nhận không vượt quá. Tỷ lệ nhận tính theo kế hoạch; nếu thiếu tin chưa gửi, cần phân biệt với lượt nhận bị thiếu thực sự.

## 7. Lỗi phát hiện và Retest

Lần đầu LOAD_03: 20/20 kết nối, 146/150 tin gửi, 0 tin gửi lỗi, 4 tin chưa gửi, 2774/2850 lượt nhận, throughput 4.85 msg/s, thời gian 30.09 s. Kết quả FAIL theo kế hoạch. Mọi lượt nhận dự kiến từ 146 tin đã gửi đều có đủ (146 × 19 = 2774); chưa có bằng chứng Backend làm mất tin.

Nguyên nhân được xác định trong công cụ tạo tải: lịch gửi dựa trên thời gian thực tế của từng vòng lặp có thể tích lũy sai lệch. Đã sửa:

```csharp
// Cu
nextSendMs = loadWatch.Elapsed.TotalMilliseconds + intervalMs;

// Moi
nextSendMs += intervalMs;
```

Sau khi sửa, LOAD_03 Retest đạt 150/150 tin và 2850/2850 lượt nhận. LOAD_04, LOAD_05 và các lần Retest LOAD_01, LOAD_02 đều PASS. Giữ kết quả FAIL ban đầu để theo dõi quá trình kiểm thử. Lịch cố định có thể gửi bù khi trễ; đây vẫn là một client gửi tuần tự, không phải bộ phát tải có tốc độ độc lập với phản hồi.

## 8. Kết luận và giới hạn

- 13/13 test case chức năng và 5/5 mức tải dự kiến đạt trong các lần chạy cuối.
- Đã quan sát 100 kết nối đồng thời và 20 tin/giây trong 60 giây, với 118800 lượt nhận trên Local.
- Không suy rộng thành 100 người dùng độc lập, giới hạn tải tối đa, SLA hay hiệu năng Azure.
- Chưa đo CPU/RAM trong lúc chạy, chưa thực hiện kiểm thử kéo dài hoặc lặp nhiều lần để đánh giá tính ổn định thống kê.
- Các kết nối nhận đều dùng tài khoản B2, chỉ một client gửi; client, Backend và database cùng máy.
- Kiểm thử không phát tin trái phép chỉ quan sát A trong 3 giây, chưa kiểm tra trực tiếp database hay tất cả thành viên. Kiểm thử trạng thái và xác nhận nhận tin chưa đối chiếu trực tiếp bản ghi SQL. Chưa kiểm thử giao diện, cuộc gọi WebRTC hay toàn bộ chức năng Hub.
- Mỗi lần chạy tạo thêm tin nhắn trong database thử nghiệm; không dùng dữ liệu thật.
- Không commit environment Local, JWT, mật khẩu, khóa Vault, bin/obj hoặc các thay đổi Postman ngoài phạm vi SignalR.

Sau khi kiểm thử xong, có thể xóa các biến JWT khỏi Terminal:

```powershell
Remove-Item Env:QA2_TOKEN,Env:QA2_TOKEN_B2,Env:QA2_TOKEN_D -ErrorAction SilentlyContinue
```

## 9. File thuộc phạm vi PR

- 4 request `TC_SIGNALR_01` đến `TC_SIGNALR_04` trong collection Postman.
- `tests/Connext.QA2.SignalR/Program.cs` và `.csproj`.
- `tests/Connext.QA2.SignalR.LoadTest/Program.cs` và `.csproj`.
- `docs/qa2-signalr-testing.md`.

Tham chiếu Issue #32 và #50; không đóng issue nếu các phần nhiệm vụ khác chưa được xác nhận hoàn thành.
