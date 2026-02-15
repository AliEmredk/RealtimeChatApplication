using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.EfRealtime;

//this is the "ISseBackplane backplane" dependency we are using to access the connections,All of the clients connected to the api,
//they will store in to the backplane and when we need to send thing to the connection we use this as a high level abstraction
//this is a primary constructor, you can seperate it by changing it to an explicit constructor

[ApiController]
[Route("")]
public class RealtimeController(ISseBackplane backplane, IRealtimeManager realtimeManager, AppDb db)
    : RealtimeControllerBase(backplane)
{
    /// <summary>
    ///Will produce the following in the browser's response tab:
    ///id: 2
    ///event: messages
    ///data: [{"id":1,"content":"hi","createdAt":"2026-02-09T10:34:37.1856196+00:00"},{"id":2,"content":"asd","createdAt":"2026-02-09T10:34:40.5670584+00:00"},{"id":3,"content":"a","createdAt":"2026-02-09T11:11:34.6666671+00:00"}]
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    [HttpGet("messages")]
    public async Task<RealtimeListenResponse<List<Message>>> GetMessages(string connectionId)
    {
        var group = "messages";

        await backplane.Groups.AddToGroupAsync(connectionId, group);
        await backplane.Groups.AddToGroupAsync(connectionId, $"client:{connectionId}"); // 👈

        realtimeManager.Subscribe<AppDb>(connectionId, group,
            criteria: changes => changes.HasChanges<Message>(),
            query: async ctx => await ctx.Messages.OrderBy(m => m.CreatedAt).ToListAsync());

        return new RealtimeListenResponse<List<Message>>(group,
            await db.Messages.OrderBy(m => m.CreatedAt).ToListAsync());
    }

    /// <summary>
    /// Since this calls .SaveChangesAsync() on dbcontext, it triggers the "Listener" to make a new query and broadcast to the group
    /// </summary>
    /// <param name="message"></param>
    [HttpPost("send")]
    public async Task Send(string message)
    {
        db.Messages.Add(new Message { Content = message });
        await db.SaveChangesAsync();
    }

    [HttpPost("poke")]
    public async Task<IActionResult> Poke([FromQuery] string connectionId)
    {
        await backplane.Clients.SendToGroupAsync($"client:{connectionId}", new
        {
            type = "poke",
            message = "You got poked!!",
            at = DateTimeOffset.UtcNow
        });

        return Ok();
    }

    [HttpPost("room/join")]
    public async Task<IActionResult> JoinRoom(
        [FromQuery] string connectionId,
        [FromQuery] string room)
    {
        var group = $"room:{room}";

        // 1) Get existing members BEFORE adding the new one
        var members = await backplane.Groups.GetMembersAsync(group);
        
        // 2) Add joiner to the room
        await backplane.Groups.AddToGroupAsync(connectionId, group);
        
        // 3) Notify existing members only
        var evt = new
        {
            type = "system",
            message = $"{connectionId} entered '{room}'",
            room,
            at = DateTimeOffset.UtcNow
        };

        foreach (var memberId in members)
        {
            await backplane.Clients.SendToGroupAsync($"client:{memberId}", evt);
        }

        return Ok(new { joined = room, connectionId });
    }

    [HttpPost("room/leave")]
    public async Task<IActionResult> LeaveRoom(
        [FromQuery] string connectionId,
        [FromQuery] string room)
    {
        var group = $"room:{room}";

        // members before removal
        var members = await backplane.Groups.GetMembersAsync(group);

        // remove from group
        await backplane.Groups.RemoveFromGroupAsync(connectionId, group);

        var evt = new
        {
            type = "system",
            message = $"{connectionId} left '{room}'",
            room,
            at = DateTimeOffset.UtcNow
        };

        foreach (var memberId in members)
        {
            if (memberId == connectionId) continue;
            await backplane.Clients.SendToGroupAsync($"client:{memberId}", evt);
        }

        return Ok(new { left = room, connectionId });
    }

}