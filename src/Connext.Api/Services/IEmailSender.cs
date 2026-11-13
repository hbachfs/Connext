namespace Connext.Api.Services;

public interface IEmailSender
{
    Task SendPasswordResetCodeAsync(string email, string code);
}
