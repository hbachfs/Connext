using System.Net;
using System.Net.Mail;

namespace Connext.Api.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        // Smtp:User / Smtp:Password lấy từ User Secrets hoặc biến môi trường (Smtp__User, Smtp__Password), KHÔNG để trong file commit.
        var smtpConfig = _config.GetSection("Smtp");
        var host = smtpConfig["Host"];
        var portText = smtpConfig["Port"];
        var user = smtpConfig["User"];
        var password = smtpConfig["Password"];
        var from = smtpConfig["From"];

        if (string.IsNullOrWhiteSpace(host) ||
            !int.TryParse(portText, out var port) || port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình Smtp hợp lệ (Host/Port/User/Password/From). " +
                "Đặt các giá trị bằng User Secrets hoặc biến môi trường Smtp__*. ");
        }

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, password),
            EnableSsl = true
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(from, smtpConfig["FromName"]),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage);
    }
}
