namespace Connext.Api.Entities;

public class PasswordResetOtp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string CodeHash { get; set; } // Mã 6 số đã băm
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; } = 0;
    public bool IsUsed { get; set; } = false;
}
