using api.Contracts;
using dataaccess;
using dataaccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace api.Services;

public class AuthService : IAuthService
{
    private readonly MyDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthService(MyDbContext db, IPasswordHasher<AppUser> hasher, IJwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var username = req.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(req.Password))
            throw new ArgumentException("Username and password are required.");

        var exists = await _db.AppUsers.AnyAsync(u => u.Username == username);
        if (exists)
            throw new InvalidOperationException("Username already exists.");

        var user = new AppUser
        {
            Username = username,
            PasswordHash = "temp",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        var roles = await GetRolesAsync(user.Id);
        var token = _jwt.CreateToken(user, roles);
        return new AuthResponse(token, user.Id.ToString(), user.Username);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var username = req.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(req.Password))
            throw new ArgumentException("Username and password are required.");

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid username/password.");

        var ok = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (ok == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid username/password.");

        var roles = await GetRolesAsync(user.Id);
        var token = _jwt.CreateToken(user, roles);
        return new AuthResponse(token, user.Id.ToString(), user.Username);
    }
    
    private async Task<List<string>> GetRolesAsync(Guid userId)
    {
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync();
    }
}