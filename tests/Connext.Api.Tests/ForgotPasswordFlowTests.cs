// Test luồng quên mật khẩu qua AuthController với SQLite in-memory (ExecuteUpdateAsync không chạy trên provider InMemory).
using System.Text.RegularExpressions;
using Connext.Api.Controllers;
using Connext.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Connext.Api.Tests;

public sealed class ForgotPasswordFlowTests : IDisposable
{
    private sealed class CapturingMail : IEmailService
    {
        public List<(string To, string Body)> Sent { get; } = new();
        public Task SendEmailAsync(string to, string subject, string body) { lock (Sent) Sent.Add((to, body)); return Task.CompletedTask; }
    }

    private const string NewPassword = "NewPass123";
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero));
    private readonly CapturingMail _mail = new();
    private readonly ServiceProvider _sp;
    private readonly Guid _aliceId = Guid.NewGuid();
    private readonly Guid _bobId = Guid.NewGuid();

    public ForgotPasswordFlowTests()
    {
        _conn.Open();
        var services = new ServiceCollection();
        services.AddSingleton<IEmailService>(_mail);
        _sp = services.BuildServiceProvider();

        using var db = NewDb();
        db.Database.EnsureCreated();
        db.Users.Add(new User { Id = _aliceId, FullName = "Alice", Phone = "0900000001", Email = "alice@x.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass1") });
        db.Users.Add(new User { Id = _bobId, FullName = "Bob", Phone = "0900000002", Email = "bob@x.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass2") });
        db.SaveChanges();
    }

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);

    private AuthController NewController() =>
        new(NewDb(), TestConfig.Jwt(_time), _sp.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<AuthController>.Instance);

    private static string Msg(IActionResult r) => r switch
    {
        OkObjectResult { Value: ApiResponse<object> a } => a.Message,
        BadRequestObjectResult { Value: ApiResponse<object> a } => a.Message,
        _ => throw new InvalidOperationException(r.GetType().Name),
    };

    private static T Data<T>(IActionResult r) where T : class =>
        (T)(((OkObjectResult)r).Value as ApiResponse<object>)!.Data!;

    private async Task<string> LastOtpAsync(string to)
    {
        for (var i = 0; i < 100 && !_mail.Sent.Any(m => m.To == to); i++) await Task.Delay(20);
        var m = Regex.Match(_mail.Sent.Last(x => x.To == to).Body, @"<b>(\d{6})</b>");
        Assert.True(m.Success, "OTP trong email phải đúng 6 chữ số");
        return m.Groups[1].Value;
    }

    private async Task<string> VerifyAsync(string email, string otp)
    {
        var r = await NewController().VerifyOtp(new VerifyOtpRequest { Email = email, Otp = otp });
        var data = Data<object>(r);
        return (string)data.GetType().GetProperty("resetToken")!.GetValue(data)!;
    }

    private bool PasswordIs(Guid id, string pw)
    {
        using var db = NewDb();
        return BCrypt.Net.BCrypt.Verify(pw, db.Users.AsNoTracking().Single(u => u.Id == id).PasswordHash);
    }

    private Task<IActionResult> Reset(string token, string pw = NewPassword) =>
        NewController().ResetPassword(new ResetPasswordRequest { ResetToken = token, NewPassword = pw, ConfirmPassword = pw });

    // ------------------------------------------------------------------ không lộ email
    [Fact]
    public async Task Forgot_password_response_is_identical_for_existing_and_unknown_email()
    {
        var known = await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        var unknown = await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "nobody@x.com" });
        Assert.IsType<OkObjectResult>(known);
        Assert.IsType<OkObjectResult>(unknown);
        Assert.Equal(Msg(known), Msg(unknown));
    }

    [Fact]
    public async Task Unknown_email_creates_no_otp_and_sends_no_mail()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "nobody@x.com" });
        await Task.Delay(100);
        using var db = NewDb();
        Assert.Empty(db.PasswordResetOtps);
        Assert.Empty(_mail.Sent);
    }

    // ------------------------------------------------------------------ luồng đúng
    [Fact]
    public async Task Full_flow_changes_password_and_token_works_only_once()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "ALICE@x.com" }); // không phân biệt hoa/thường
        var token = await VerifyAsync("alice@x.com", await LastOtpAsync("alice@x.com"));

        Assert.IsType<OkObjectResult>(await Reset(token));
        Assert.True(PasswordIs(_aliceId, NewPassword));
        Assert.IsType<BadRequestObjectResult>(await Reset(token, "Another123")); // dùng lại token -> bị từ chối
        Assert.True(PasswordIs(_aliceId, NewPassword));
    }

    // ------------------------------------------------------------------ token giả / sai
    [Fact]
    public async Task Forged_token_cannot_reset_anyones_password()   // đúng kịch bản trong issue
    {
        var attacker = TestConfig.Jwt(_time, "attacker-key-attacker-key-attacker-key!!");
        var forged = attacker.GenerateResetToken(_bobId, Guid.NewGuid());
        Assert.IsType<BadRequestObjectResult>(await Reset(forged, "Hacked1234"));
        Assert.True(PasswordIs(_bobId, "OldPass2"));
    }

    [Fact]
    public async Task Valid_signature_but_otp_never_verified_is_rejected()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        Guid otpId;
        using (var db = NewDb()) otpId = db.PasswordResetOtps.Single().Id;
        var token = TestConfig.Jwt(_time).GenerateResetToken(_aliceId, otpId); // chữ ký đúng nhưng chưa qua verify-otp
        Assert.IsType<BadRequestObjectResult>(await Reset(token));
        Assert.True(PasswordIs(_aliceId, "OldPass1"));
    }

    [Fact]
    public async Task Token_of_alice_cannot_be_combined_with_otp_of_bob()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "bob@x.com" });
        var bobsOtpId = await NewDb().PasswordResetOtps.Select(o => o.Id).SingleAsync();
        var token = TestConfig.Jwt(_time).GenerateResetToken(_aliceId, bobsOtpId);
        Assert.IsType<BadRequestObjectResult>(await Reset(token));
        Assert.True(PasswordIs(_aliceId, "OldPass1"));
    }

    // ------------------------------------------------------------------ OTP
    [Fact]
    public async Task Five_wrong_codes_lock_the_otp_even_for_the_correct_code()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        var code = await LastOtpAsync("alice@x.com");
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.IsType<BadRequestObjectResult>(await NewController().VerifyOtp(new VerifyOtpRequest { Email = "alice@x.com", Otp = wrong }));
        Assert.IsType<BadRequestObjectResult>(await NewController().VerifyOtp(new VerifyOtpRequest { Email = "alice@x.com", Otp = code }));
    }

    [Fact]
    public async Task Failure_messages_do_not_reveal_the_reason()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        var code = await LastOtpAsync("alice@x.com");
        var wrongOtp = Msg(await NewController().VerifyOtp(new VerifyOtpRequest { Email = "alice@x.com", Otp = code == "000000" ? "111111" : "000000" }));
        var wrongEmail = Msg(await NewController().VerifyOtp(new VerifyOtpRequest { Email = "nobody@x.com", Otp = "123456" }));
        Assert.Equal(wrongOtp, wrongEmail);
    }

    [Fact]
    public async Task Expired_code_is_rejected()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        var code = await LastOtpAsync("alice@x.com");
        _time.Now = _time.Now.AddMinutes(6);
        Assert.IsType<BadRequestObjectResult>(await NewController().VerifyOtp(new VerifyOtpRequest { Email = "alice@x.com", Otp = code }));
    }

    [Fact]
    public async Task Resend_is_limited_by_cooldown_and_hourly_cap()
    {
        var c = () => NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        await c();
        await c();                                                   // trong 60 giây -> bỏ qua
        using (var db = NewDb()) Assert.Single(db.PasswordResetOtps);

        for (var i = 0; i < 10; i++) { _time.Now = _time.Now.AddSeconds(61); await c(); }
        using (var db = NewDb()) Assert.Equal(5, db.PasswordResetOtps.Count()); // tối đa 5 mã / giờ
    }

    [Fact]
    public async Task A_new_code_invalidates_the_previous_one()
    {
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        var first = await LastOtpAsync("alice@x.com");
        _time.Now = _time.Now.AddSeconds(61);
        await NewController().ForgotPassword(new ForgotPasswordRequest { Email = "alice@x.com" });
        Assert.IsType<BadRequestObjectResult>(await NewController().VerifyOtp(new VerifyOtpRequest { Email = "alice@x.com", Otp = first }));
    }

    public void Dispose() { _sp.Dispose(); _conn.Dispose(); }
}

