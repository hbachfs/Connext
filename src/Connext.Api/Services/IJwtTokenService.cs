using Connext.Data.Entities;

namespace Connext.Api.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}

