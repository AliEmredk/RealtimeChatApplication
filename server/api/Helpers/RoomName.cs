namespace api.Helpers;

public static class RoomName
{
    public static string Normalize(string roomName)
        => (roomName ?? "").Trim().ToLowerInvariant();
}