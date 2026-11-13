# Connext - Documentation

Thư mục chứa tài liệu đặc tả yêu cầu phần mềm (SRS), sơ đồ kiến trúc, UML và báo cáo đồ án.
## Chat realtime (SignalR)

### Yêu cầu
- Bật `MapHub<ChatHub>("/hubs/chat")` trong `Program.cs`
- Cho phép token JWT qua query string cho kết nối WebSocket

### Backend (`Program.cs`)

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // ... TokenValidationParameters ...

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR();

// ...
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
```

### Frontend (`wwwroot/js/common.js`)

```javascript
const chatConnection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat", {
        accessTokenFactory: () => localStorage.getItem("jwt")
    })
    .withAutomaticReconnect()
    .build();

chatConnection.on("ReceiveMessage", (user, message) => {
    console.log(`${user}: ${message}`);
});

chatConnection.start();
```

### Lưu ý
- `UseAuthentication()` phải đứng trước `UseAuthorization()` và `MapHub`.
- Chỉ nhận token từ query string ở path `/hubs/chat`, và nên dùng HTTPS.