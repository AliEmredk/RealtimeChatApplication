namespace api.Contracts;

public sealed record MessageDto(
    Guid Id,
    string Room,
    string SenderUsername,
    string Type,
    string Content,
    Guid? RecipientUserId,
    DateTime SentAt
    );
    
    public sealed record CreateMessageRequest(
        string Content
        );