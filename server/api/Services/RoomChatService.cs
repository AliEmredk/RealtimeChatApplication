using api.Contracts;
using api.Helpers;
using dataaccess;
using dataaccess.Entities;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;

namespace api.Services;

//Learn later what does sealed class do??

public sealed class RoomChatService : IRoomChatService
{
    private readonly MyDbContext _db;
    private readonly ISseBackplane _bp;

    public RoomChatService(MyDbContext db, ISseBackplane bp)
    {
        _db = db;
        _bp = bp;
    }

    public async Task<List<MessageDto>> GetLastMessagesAsync(
        string roomName,
        int take,
        Guid? currentUserId)
    {
        take = Math.Clamp(take, 1, 50);
        roomName = RoomName.Normalize(roomName);

        var room = await _db.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roomName);

        if (room is null) return new();

        var msgs = await _db.Messages
            .AsNoTracking()
            .Where(m =>
                m.RoomId == room.Id &&
                (
                    m.Type == MessageType.Public ||

                    // if DM → only sender or recipient can see it
                    (m.Type == MessageType.Dm &&
                     currentUserId != null &&
                     (m.SenderUserId == currentUserId ||
                      m.RecipientUserId == currentUserId))
                )
            )
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.Type,
                m.Content,
                m.RecipientUserId,
                m.SentAt,
                SenderUsername = m.SenderUser.Username
            })
            .ToListAsync();

        msgs.Reverse();

        return msgs.Select(m => new MessageDto(
            Id: m.Id,
            Room: roomName,
            SenderUsername: m.SenderUsername,
            Type: m.Type.ToString().ToLowerInvariant(),
            Content: m.Content,
            RecipientUserId: m.RecipientUserId,
            SentAt: m.SentAt
        )).ToList();
    }


    public async Task<MessageDto> PostPublicMessageAsync(string roomName, Guid senderUserId, string content)
    {
        roomName = RoomName.Normalize(roomName);

        content = content?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.", nameof(content));

        // ✅ Load the real authenticated user
        var sender = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == senderUserId);
        if (sender is null || sender.IsActive == false)
            throw new UnauthorizedAccessException("User not found or inactive.");

        var room = await GetOrCreateRoomAsync(roomName);

        var msg = new Message
        {
            RoomId = room.Id,
            SenderUserId = sender.Id,          // ✅ real sender
            Type = MessageType.Public,
            Content = content,
            RecipientUserId = null,
            SentAt = DateTime.UtcNow
        };

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new MessageDto(
            Id: msg.Id,
            Room: roomName,
            SenderUsername: sender.Username,
            Type: "public",
            Content: msg.Content,
            RecipientUserId: null,
            SentAt: msg.SentAt
        );

        //await _bp.Clients.SendToGroupAsync($"room:{roomName}", dto);
        return dto;
    }

    private async Task<Room> GetOrCreateRoomAsync(string roomName)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Name == roomName);
        if (room is not null) return room;

        room = new Room
        {
            Name = roomName,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return room;
    }
    
    public async Task<MessageDto> PostDmAsync(string roomName, Guid senderUserId, Guid recipientUserId, string content)
{
    roomName = RoomName.Normalize(roomName);

    content = content?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(content))
        throw new ArgumentException("Content is required.", nameof(content));

    if (senderUserId == recipientUserId)
        throw new ArgumentException("You cannot DM yourself.");

    var sender = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == senderUserId);
    if (sender is null || !sender.IsActive)
        throw new UnauthorizedAccessException("Sender not found or inactive.");

    var recipient = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == recipientUserId);
    if (recipient is null || !recipient.IsActive)
        throw new ArgumentException("Recipient not found or inactive.");

    // ✅ extra safety: enforce admin in DB too (not only token)
    var isAdmin = await _db.UserRoles
        .AnyAsync(ur => ur.UserId == senderUserId && ur.Role.Name == "Admin");

    if (!isAdmin)
        throw new UnauthorizedAccessException("Only Admin can send DMs.");

    var room = await GetOrCreateRoomAsync(roomName);

    var msg = new Message
    {
        RoomId = room.Id,
        SenderUserId = sender.Id,
        Type = MessageType.Dm,
        Content = content,
        RecipientUserId = recipient.Id,
        SentAt = DateTime.UtcNow
    };

    _db.Messages.Add(msg);
    await _db.SaveChangesAsync();

    var dto = new MessageDto(
        Id: msg.Id,
        Room: roomName,
        SenderUsername: sender.Username,
        Type: "dm",
        Content: msg.Content,
        RecipientUserId: msg.RecipientUserId,
        SentAt: msg.SentAt
    );

    // 🚫 DO NOT send DM to room group (that leaks it to everyone)
    // ✅ Send DM to sender + recipient user channels/groups:
    await _bp.Clients.SendToGroupAsync($"user:{sender.Id}", dto);
    await _bp.Clients.SendToGroupAsync($"user:{recipient.Id}", dto);

    return dto;
}
    
    public async Task<string> CreateRoomAsync(string roomName)
    {
        roomName = RoomName.Normalize(roomName);

        if (string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("Room name is required.");

        if (roomName.Length > 50)
            throw new ArgumentException("Room name is too long (max 50).");

        var exists = await _db.Rooms.AnyAsync(r => r.Name == roomName);
        if (exists)
            throw new ArgumentException("Room already exists.");

        var room = new Room
        {
            Name = roomName,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        return roomName;
    }

    public async Task<string> ArchiveRoomAsync(string roomName)
    {
        roomName = RoomName.Normalize(roomName);

        if (string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("Room name is required.");

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Name == roomName);

        if (room is null)
            throw new ArgumentException("Room not found.");

        if (room.IsArchived)
            return roomName; // already archived, idempotent

        room.IsArchived = true;
        await _db.SaveChangesAsync();

        return roomName;
    }
    
    public async Task<int> GetOnlineCountAsync(string roomName)
    {
        roomName = RoomName.Normalize(roomName);

        // people "online" = number of SSE connections currently in this group
        var members = await _bp.Groups.GetMembersAsync($"room:{roomName}");
        return members.Count;
    }
    
    public async Task<List<UserMiniDto>> GetRoomParticipantsAsync(string roomName, int take = 50)
    {
        roomName = RoomName.Normalize(roomName);
        take = Math.Clamp(take, 1, 200);

        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Name == roomName);
        if (room is null) return new();

        // users who have sent messages in this room
        var users = await _db.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == room.Id && m.SenderUserId != null)
            .Select(m => new { m.SenderUserId, m.SenderUser.Username })
            .Distinct()
            .OrderBy(x => x.Username)
            .Take(take)
            .ToListAsync();

        return users.Select(x => new UserMiniDto(x.SenderUserId, x.Username)).ToList();
    }

}