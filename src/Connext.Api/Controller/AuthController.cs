using Connext.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Connext.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // Chính sách OTP quên mật khẩu
    private const int OtpExpireMinutes = 5;
    private const int MaxFailedAttempts = 5;
    private const int MaxSendsPerHour = 5;
    private const int ResendCooldownSeconds = 60;

    private const string ForgotOkMessage = "Nếu email tồn tại trong hệ thống, mã OTP đã được gửi đến email đó.";
    private const string OtpFailMessage = "Mã OTP không hợp lệ hoặc đã hết hạn.";
    private const string ResetFailMessage = "Phiên đổi mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng thử lại từ đầu.";

    // Dùng để tốn thời gian tương đương khi không tìm thấy tài khoản (chống dò email qua độ trễ)
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("000000");

    private readonly AppDbContext _context;
    private readonly JwtHelper _jwtHelper;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, JwtHelper jwtHelper, IServiceScopeFactory scopes,
        TimeProvider time, ILogger<AuthController> logger)
    {
        _context = context;
        _jwtHelper = jwtHelper;
        _scopes = scopes;
        _time = time;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (await _context.Users.AnyAsync(u => u.Phone == req.Phone))
            return BadRequest(ApiResponse<object>.Fail("Số điện thoại đã được sử dụng."));

        if (await _context.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(ApiResponse<object>.Fail("Email đã được sử dụng."));

        var user = new User
        {
            FullName = req.FullName,
            Phone = req.Phone,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Đăng ký thành công."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == req.Phone);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return BadRequest(ApiResponse<object>.Fail("Số điện thoại hoặc mật khẩu không đúng."));

        var token = _jwtHelper.GenerateToken(user);

        return Ok(ApiResponse<object>.Ok(new
        {
            token,
            user = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Gender = user.Gender
            }
        }, "Đăng nhập thành công."));
    }

    // ============================== QUÊN MẬT KHẨU ==============================

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // OTP 6 số (000000-999999) từ bộ sinh ngẫu nhiên mật mã, KHÔNG dùng System.Random.
    private static string GenerateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        // Luôn trả CÙNG một phản hồi dù email có tồn tại hay không (không lộ danh sách tài khoản).
        var email = NormalizeEmail(req.Email);
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null)
        {
            BCrypt.Net.BCrypt.HashPassword(GenerateOtp()); // tốn thời gian tương đương nhánh có tài khoản
            return Ok(ApiResponse<object>.Ok(null, ForgotOkMessage));
        }

        var now = _time.GetUtcNow().UtcDateTime;

        // Giới hạn gửi lại: cách nhau tối thiểu 60 giây và tối đa 5 mã/giờ cho mỗi tài khoản.
        var sentLastHour = await _context.PasswordResetOtps.AsNoTracking()
            .Where(o => o.Email == user.Email && o.CreatedAt > now.AddHours(-1))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.CreatedAt)
            .ToListAsync();

        if (sentLastHour.Count >= MaxSendsPerHour ||
            (sentLastHour.Count > 0 && (now - sentLastHour[0]).TotalSeconds < ResendCooldownSeconds))
            return Ok(ApiResponse<object>.Ok(null, ForgotOkMessage));

        // Mã mới vô hiệu mọi mã cũ (không cộng dồn số lần đoán bằng cách gửi lại liên tục).
        await _context.PasswordResetOtps
            .Where(o => o.Email == user.Email && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));

        var otp = GenerateOtp();
        _context.PasswordResetOtps.Add(new PasswordResetOtp
        {
            Email = user.Email,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(otp),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(OtpExpireMinutes)
        });
        await _context.SaveChangesAsync();

        SendOtpInBackground(user.Email, otp);

        return Ok(ApiResponse<object>.Ok(null, ForgotOkMessage));
    }

    // Gửi mail chạy nền để thời gian phản hồi không để lộ email có tồn tại; lỗi SMTP chỉ ghi log (không ghi email/OTP).
    private void SendOtpInBackground(string toEmail, string otp)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var mail = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await mail.SendEmailAsync(toEmail, "Mã xác thực quên mật khẩu - Connext",
                    $"Mã OTP của bạn là: <b>{otp}</b>. Mã này sẽ hết hạn sau {OtpExpireMinutes} phút. " +
                    "Nếu không phải bạn yêu cầu, hãy bỏ qua email này.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email OTP thất bại. Kiểm tra cấu hình Smtp.");
            }
        });
    }

    [HttpPost("verify-otp")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest req)
    {
        // Mọi lỗi đều trả cùng một thông báo (không phân biệt email sai / OTP sai / hết hạn / bị khóa).
        var email = NormalizeEmail(req.Email);
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
        {
            BCrypt.Net.BCrypt.Verify(req.Otp, DummyHash);
            return BadRequest(ApiResponse<object>.Fail(OtpFailMessage));
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var otpRecord = await _context.PasswordResetOtps
            .Where(o => o.Email == user.Email && !o.IsUsed && !o.IsVerified
                        && o.ExpiresAt > now && o.FailedAttempts < MaxFailedAttempts)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null)
        {
            BCrypt.Net.BCrypt.Verify(req.Otp, DummyHash);
            return BadRequest(ApiResponse<object>.Fail(OtpFailMessage));
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Otp, otpRecord.CodeHash))
        {
            // Tăng bộ đếm bằng 1 câu lệnh nguyên tử (gửi song song nhiều request cũng không vượt được giới hạn)
            await _context.PasswordResetOtps.Where(o => o.Id == otpRecord.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.FailedAttempts, o => o.FailedAttempts + 1));
            // Đủ số lần sai thì khóa luôn mã này
            await _context.PasswordResetOtps.Where(o => o.Id == otpRecord.Id && o.FailedAttempts >= MaxFailedAttempts)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));
            return BadRequest(ApiResponse<object>.Fail(OtpFailMessage));
        }

        // OTP đúng -> đánh dấu "đã xác thực" (CHƯA phải đã dùng: mã chỉ hết giá trị khi đổi mật khẩu xong).
        // Chỉ 1 request thắng nếu gửi song song.
        var verified = await _context.PasswordResetOtps
            .Where(o => o.Id == otpRecord.Id && !o.IsVerified && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsVerified, true));
        if (verified == 0)
            return BadRequest(ApiResponse<object>.Fail(OtpFailMessage));

        var resetToken = _jwtHelper.GenerateResetToken(user.Id, otpRecord.Id);
        return Ok(ApiResponse<object>.Ok(new { resetToken }, "Xác thực thành công."));
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        // Kiểm tra chữ ký + hạn + issuer + audience + purpose. (Trước đây dùng ReadJwtToken: KHÔNG kiểm tra chữ ký.)
        var payload = _jwtHelper.ValidateResetToken(req.ResetToken);
        if (payload == null)
            return BadRequest(ApiResponse<object>.Fail(ResetFailMessage));

        await using var tx = await _context.Database.BeginTransactionAsync();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == payload.UserId);
        if (user == null)
            return BadRequest(ApiResponse<object>.Fail(ResetFailMessage));

        // Dùng 1 lần: OTP trong token phải đang "đã xác thực, chưa dùng" và thuộc đúng tài khoản này.
        // Câu lệnh nguyên tử: 2 request song song cùng 1 token thì chỉ 1 cái thành công.
        var consumed = await _context.PasswordResetOtps
            .Where(o => o.Id == payload.OtpId && o.Email == user.Email && o.IsVerified && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));
        if (consumed == 0)
            return BadRequest(ApiResponse<object>.Fail(ResetFailMessage));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _context.SaveChangesAsync();

        // Hủy mọi mã khác còn hiệu lực của tài khoản này
        await _context.PasswordResetOtps
            .Where(o => o.Email == user.Email && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));

        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."));
    }
}

