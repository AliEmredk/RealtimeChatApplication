namespace dataaccess.Entities;

public class Message
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = default!;

    public Guid SenderUserId { get; set; }
    public AppUser SenderUser { get; set; } = default!;

    public MessageType Type { get; set; }

    public string Content { get; set; } = default!;

    // Only used for DM
    public Guid? RecipientUserId { get; set; }
    public AppUser? RecipientUser { get; set; }

    public DateTime SentAt { get; set; }
}