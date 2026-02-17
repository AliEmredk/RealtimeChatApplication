using api.Contracts;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using api.Helpers;

namespace api.Controllers;



[ApiController]
[Route("rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomChatService _svc;

    public RoomsController(IRoomChatService svc)
    {
        _svc = svc;
    }


    // GET/rooms/{roomName}/messages
    [HttpGet("{roomName}/messages")]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(string roomName, int take = 5)
        => Ok(await _svc.GetLastMessagesAsync(roomName, take));

    [HttpPost("{roomName}/messages")]
    public async Task<ActionResult<MessageDto>> PostMessage(string roomName, [FromBody] CreateMessageRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest("Content is required");

        try
        {
            var dto = await _svc.PostPublicMessageAsync(roomName, req.Content);
            return Created($"/rooms/{roomName}/messages?take=1", dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    // GET /rooms/{roomName}/listen
    [HttpGet("{roomName}/listen")]
    public Task Listen(string roomName, CancellationToken ct, [FromServices] ISseBackplane bp)
    {
        roomName = RoomName.Normalize(roomName);
        return HttpContext.StreamSseAsync(bp, new[] { $"room:{roomName}" }, ct);
    }
 

}