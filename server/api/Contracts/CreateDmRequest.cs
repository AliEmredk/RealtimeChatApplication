namespace api.Contracts;

public record CreateDmRequest(Guid RecipientUserId, string Content);