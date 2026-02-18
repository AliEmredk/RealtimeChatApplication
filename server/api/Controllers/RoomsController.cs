using System.IdentityModel.Tokens.Jwt;
using api.Contracts;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using api.Helpers;
using System.Security.Claims;
using dataaccess;
using Microsoft.AspNetCore.Authorization;

namespace api.Controllers;



[ApiController]
[Route("rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomChatService _svc;

    public RoomsController(IRoomChatService svc) => _svc = svc;

    [HttpGet("{roomName}/messages")]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(
        string roomName,
        int take = 5)
    {
        roomName = RoomName.Normalize(roomName);

        Guid? userId = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdStr =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (Guid.TryParse(userIdStr, out var parsed))
                userId = parsed;
        }

        return Ok(await _svc.GetLastMessagesAsync(roomName, take, userId));
    }


    
    [HttpGet]
    public async Task<ActionResult<List<string>>> GetRooms([FromServices] MyDbContext db)
    {
        var rooms = await db.Rooms
            .AsNoTracking()
            .Where(r => !r.IsArchived)   //only active rooms
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync();

        return Ok(rooms);
    }


    [Authorize]
    [HttpPost("{roomName}/messages")]
    public async Task<ActionResult<MessageDto>> PostMessage(
        string roomName,
        [FromBody] CreateMessageRequest req,
        [FromServices] ISseBackplane bp,
        CancellationToken ct)
    {
        roomName = RoomName.Normalize(roomName);

        if (req is null || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest("Content is required");

        var userIdStr =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userIdStr))
            return Unauthorized("Missing user id claim.");

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("Invalid user id claim.");

        var dto = await _svc.PostPublicMessageAsync(roomName, userId, req.Content);
        
        
        await bp.Clients.SendToGroupAsync($"room:{roomName}", dto);

        return Ok(dto);
    }


    [HttpGet("{roomName}/listen")]
    public Task Listen(string roomName, CancellationToken ct, [FromServices] ISseBackplane bp)
    {
        roomName = RoomName.Normalize(roomName);
        return HttpContext.StreamSseAsync(bp, new[] { $"room:{roomName}" }, ct);
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost("{roomName}/dm")]
    public async Task<ActionResult<MessageDto>> SendDm(string roomName, [FromBody] CreateDmRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest("Content is required");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var senderId))
            return Unauthorized("Invalid user id claim.");

        try
        {
            var dto = await _svc.PostDmAsync(roomName, senderId, req.RecipientUserId, req.Content);
            return Ok(dto);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
    }
    
    [Authorize]
    [HttpGet("{roomName}/listen-auth")]
    public Task ListenAuth(string roomName, CancellationToken ct, [FromServices] ISseBackplane bp)
    {
        roomName = RoomName.Normalize(roomName);

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userIdStr))
            throw new UnauthorizedAccessException("Missing user id claim.");

        var groups = new[] { $"room:{roomName}", $"user:{userIdStr}" };
        return HttpContext.StreamSseAsync(bp, groups, ct);
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CreateRoomResponse>> CreateRoom([FromBody] CreateRoomRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        try
        {
            var createdName = await _svc.CreateRoomAsync(req.Name);
            return Ok(new CreateRoomResponse(createdName));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{roomName}/archive")]
    public async Task<ActionResult<ArchiveRoomResponse>> ArchiveRoom(string roomName)
    {
        roomName = RoomName.Normalize(roomName);

        try
        {
            var name = await _svc.ArchiveRoomAsync(roomName);
            return Ok(new ArchiveRoomResponse(name, true));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    [HttpGet("{roomName}/online")]
    public async Task<ActionResult<object>> GetOnline(string roomName)
    {
        roomName = RoomName.Normalize(roomName);
        var count = await _svc.GetOnlineCountAsync(roomName);
        return Ok(new { room = roomName, online = count });
    }
    
    [Authorize(Roles = "Admin")]
    [HttpGet("{roomName}/participants")]
    public async Task<ActionResult<List<UserMiniDto>>> GetParticipants(string roomName, int take = 50)
    {
        roomName = RoomName.Normalize(roomName);
        return Ok(await _svc.GetRoomParticipantsAsync(roomName, take));
    }
}