namespace api.Contracts;

public record CreateRoomRequest(string Name);

public sealed record ArchiveRoomResponse(string Name, bool IsArchived);