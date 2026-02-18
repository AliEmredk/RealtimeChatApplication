using api.Contracts;

namespace api.Services;

public interface IRoomChatService
{
    Task<List<MessageDto>> GetLastMessagesAsync(string roomName, int take);
    Task<MessageDto> PostPublicMessageAsync(string roomName, Guid senderUserId, string content);
    
    Task<MessageDto> PostDmAsync(string roomName, Guid senderUserId, Guid recipientUserId, string content);

    Task<string> CreateRoomAsync(string roomName);
    
    Task<string> ArchiveRoomAsync(string roomName);
    
    Task<int> GetOnlineCountAsync(string roomName);

}