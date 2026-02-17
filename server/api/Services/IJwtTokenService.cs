using dataaccess.Entities;

namespace api.Services;

public interface IJwtTokenService
{
    string CreateToken(AppUser user, IReadOnlyList<string> roles);
}