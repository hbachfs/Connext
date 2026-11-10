using Connext.Api.Data;
using Connext.Api.Hubs;
using Connext.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Bắt buộc cho SignalR
    });
});

// 3. Cấu hình Authentication (JWT)
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new ArgumentNullException("Jwt:Key", "Jwt:Key chưa được cấu hình trong appsettings.json.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key phải dài tối thiểu 32 byte. Đặt bằng User Secrets hoặc biến môi trường Jwt__Key.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Đọc token từ query string cho SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Giới hạn tần suất theo IP cho các endpoint quên mật khẩu (chống spam/brute-force).
// Lưu ý: nếu chạy sau reverse proxy/Nginx cần UseForwardedHeaders để lấy đúng IP người dùng.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("password-reset", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

// Môi trường dev: tự áp migration để có database ngay (production chạy `dotnet ef database update` thủ công)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Tạo thư mục wwwroot/uploads tự động nếu chưa có
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Chú ý thứ tự Middleware bắt buộc:
app.UseStaticFiles(new StaticFileOptions
{
    // Phục vụ file ảnh/file (wwwroot); cấm trình duyệt tự đoán kiểu nội dung (chống XSS qua file upload)
    OnPrepareResponse = ctx => ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
});
app.UseCors("CorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();