using Connext.Data.Entities;
using System.Security.Claims;

namespace Connext.Api.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
    string GeneratePasswordResetToken(User user, string tokenId, DateTime expiresAt);
    ClaimsPrincipal? ValidatePasswordResetToken(string token);
}
