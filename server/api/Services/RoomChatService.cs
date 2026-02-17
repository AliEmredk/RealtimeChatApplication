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
    private const string FakeSenderUsername = "demo-user";
    
    private readonly MyDbContext _db;
    private readonly ISseBackplane _bp;

    public RoomChatService(MyDbContext db, ISseBackplane bp)
    {
        _db = db;
        _bp = bp;
    }

    public async Task<List<MessageDto>> GetLastMessagesAsync(string roomName, int take)
    {
        // learn what this clamp thing is doing
        take = Math.Clamp(take, 1, 50);

        var room = await _db.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roomName);

        if (room is null) return new();

        var msgs = await _db.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == room.Id)
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

    public async Task<MessageDto> PostPublicMessageAsync(string roomName, string content)
    {
        
        roomName = RoomName.Normalize(roomName);
        
        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.", nameof(content));

        var room = await GetOrCreateRoomAsync(roomName);
        var sender = await GetOrCreateFakeSenderAsync();

        var msg = new Message
        {
            RoomId = room.Id,
            SenderUserId = sender.Id,
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

        // ✅ Broadcast to everyone listening on this room
        await _bp.Clients.SendToGroupAsync($"room:{roomName}", dto);
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

    private async Task<AppUser> GetOrCreateFakeSenderAsync()
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Username == FakeSenderUsername);
        if (user is not null) return user;

        user = new AppUser
        {
            Username = FakeSenderUsername,
            PasswordHash = "dev-only",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
    
}