using System.ComponentModel.DataAnnotations;
using dataaccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace dataaccess;

public class MyDbContext : DbContext 
{
    //Learn about this later
    public  MyDbContext(DbContextOptions<MyDbContext> options) : base(options) {}

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>(); // optional

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
          // Postgres extension + enum (if you want Postgres enum type)
    modelBuilder.HasPostgresExtension("pgcrypto");
    modelBuilder.HasPostgresEnum<MessageType>(schema: "public", name: "message_type");

    // --------------------
    // app_users
    // --------------------
    modelBuilder.Entity<AppUser>(e =>
    {
        e.ToTable("app_users");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        e.Property(x => x.Username)
            .IsRequired();

        e.HasIndex(x => x.Username)
            .IsUnique();

        e.Property(x => x.Email);
        e.HasIndex(x => x.Email)
            .IsUnique();

        e.Property(x => x.PasswordHash)
            .IsRequired();

        e.Property(x => x.IsActive)
            .HasDefaultValue(true);

        e.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        e.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("now()");
    });

    // --------------------
    // roles
    // --------------------
    modelBuilder.Entity<Role>(e =>
    {
        e.ToTable("roles");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        e.Property(x => x.Name)
            .IsRequired();

        e.HasIndex(x => x.Name)
            .IsUnique();
    });

    // --------------------
    // user_roles (join table)
    // --------------------
    modelBuilder.Entity<UserRole>(e =>
    {
        e.ToTable("user_roles");

        e.HasKey(x => new { x.UserId, x.RoleId });

        e.HasOne(x => x.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    });

    // --------------------
    // rooms
    // --------------------
    modelBuilder.Entity<Room>(e =>
    {
        e.ToTable("rooms");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        e.Property(x => x.Name)
            .IsRequired();

        e.HasIndex(x => x.Name)
            .IsUnique();

        e.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        e.Property(x => x.IsArchived)
            .HasDefaultValue(false);
    });

    // --------------------
    // messages
    // --------------------
    modelBuilder.Entity<Message>(e =>
    {
        e.ToTable("messages");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        e.Property(x => x.SentAt)
            .HasDefaultValueSql("now()");

        // enum stored as Postgres enum (message_type)
        e.Property(x => x.Type)
            .HasColumnType("message_type");

        e.Property(x => x.Content)
            .IsRequired();

        e.HasCheckConstraint("messages_content_not_blank", "length(btrim(content)) > 0");

        e.HasOne(x => x.Room)
            .WithMany(r => r.Messages)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.SenderUser)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.RecipientUser)
            .WithMany() // you can add a collection later if you want
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // DM constraint: dm => recipient_user_id not null, public => recipient_user_id null
        e.HasCheckConstraint(
            "dm_requires_recipient",
            "(type = 'dm' AND recipient_user_id IS NOT NULL) OR (type = 'public' AND recipient_user_id IS NULL)"
        );

        // Index for fast "last 5 messages in room"
        e.HasIndex(x => new { x.RoomId, x.SentAt })
            .HasDatabaseName("ix_messages_room_sentat");
    });

    // --------------------
    // refresh_tokens
    // --------------------
    modelBuilder.Entity<RefreshToken>(e =>
    {
        e.ToTable("refresh_tokens");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        e.Property(x => x.TokenHash)
            .IsRequired();

        e.Property(x => x.CreatedAt)
            .HasDefaultValueSql("now()");

        e.HasOne(x => x.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_refresh_tokens_user");
    });
    }

}