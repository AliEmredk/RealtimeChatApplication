using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dataaccess.Entities;
using Microsoft.IdentityModel.Tokens;

namespace api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _cfg;

    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    public string CreateToken(AppUser user, IReadOnlyList<string> roles)
    {
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _cfg["Jwt:Issuer"] ?? "newStartSSE";
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _cfg["Jwt:Audience"] ?? "newStartSSE";
        var key = Environment.GetEnvironmentVariable("JWT_KEY") ?? _cfg["Jwt:Key"] ?? throw new Exception("JWT key missing");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // ✅ add roles to token
        foreach (var r in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, r));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    
}