namespace dataaccess.Entities;

public class AppUser
{
    public Guid Id { get; set; }

    public string Username { set; get; } = default!;
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = default!;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    public bool IsActive { get; set; }
    
    // Navigation //ICollection is kind of a empty box to store data. Efcore uses these for relationship with related data from other table and this table
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}