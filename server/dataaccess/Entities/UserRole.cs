namespace dataaccess.Entities;

public class UserRole
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
}