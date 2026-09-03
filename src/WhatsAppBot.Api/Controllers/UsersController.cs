using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "RequireOwner")] // gestionar usuarios es cosa del dueño de la tienda, no de cualquier empleado

public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await _users.ListUsersAsync(ct);
        return Ok(users.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _users.InviteUserAsync(request.Email, request.DisplayName, request.Role, ct);
            return Ok(new InviteUserResponse(ToDto(result.User), result.TemporaryPassword));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleRequest request, CancellationToken ct)
    {
        try
        {
            await _users.ChangeRoleAsync(id, request.Role, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try
        {
            await _users.DeactivateUserAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _users.ReactivateUserAsync(id, ct);
        return NoContent();
    }

    private static UserDto ToDto(TenantUserSummary u) => new(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive);
}
