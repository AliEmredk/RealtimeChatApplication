using System.IdentityModel.Tokens.Jwt;
using api.Contracts;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using api.Helpers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace api.Controllers;



[ApiController]
[Route("rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomChatService _svc;

    public RoomsController(IRoomChatService svc) => _svc = svc;

    [HttpGet("{roomName}/messages")]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(string roomName, int take = 5)
        => Ok(await _svc.GetLastMessagesAsync(roomName, take));

    [Authorize]
    [HttpPost("{roomName}/messages")]
    public async Task<ActionResult<MessageDto>> PostMessage(string roomName, [FromBody] CreateMessageRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest("Content is required");

        // 1) Get userId from token claims
        var userIdStr =
            User.FindFirstValue(ClaimTypes.NameIdentifier)   // if you add this claim (recommended)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub); // your current "sub"

        if (string.IsNullOrWhiteSpace(userIdStr))
            return Unauthorized("Missing user id claim.");

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("Invalid user id claim.");

        try
        {
            // 2) Pass userId into service (no more fake sender!)
            var dto = await _svc.PostPublicMessageAsync(roomName, userId, req.Content);
            return Created($"/rooms/{roomName}/messages?take=1", dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
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
            return Created($"/rooms/{roomName}/messages?take=1", dto);
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
 

}