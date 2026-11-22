using System.Net;
using System.Net.Mail;

namespace Connext.Api.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendPasswordResetCodeAsync(string email, string code)
    {
        var smtp = _configuration.GetSection("Smtp");
        var host = smtp["Host"];
        var from = smtp["From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("SMTP Host and From must be configured.");

        using var message = new MailMessage(from, email)
        {
            Subject = "Mã xác minh đặt lại mật khẩu Connext",
            Body = $"Mã xác minh của bạn là {code}. Mã có hiệu lực trong 10 phút.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(host, smtp.GetValue("Port", 587))
        {
            EnableSsl = smtp.GetValue("EnableSsl", true)
        };

        var username = smtp["Username"];
        var password = smtp["Password"];
        if (!string.IsNullOrEmpty(username))
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);

        await client.SendMailAsync(message);
    }
}
