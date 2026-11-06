using System.Net;
using System.Net.Mail;

namespace Connext.Api.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var smtpConfig = _config.GetSection("Smtp");

        var client = new SmtpClient(smtpConfig["Host"], int.Parse(smtpConfig["Port"]!))
        {
            Credentials = new NetworkCredential(smtpConfig["User"], smtpConfig["Password"]),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(smtpConfig["From"]!, smtpConfig["FromName"]),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage);
    }
}
