using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Freelaverse.API.Models.Users;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [Authorize]
    [HttpPatch]
    public async Task<ActionResult<User>> UpdatePartial([FromBody] UpdateUserRequest request)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var current = await _userService.GetByIdAsync(userId);
        if (current is null) return NotFound();

        var merged = new User
        {
            Id = current.Id,
            UserName = request.UserName ?? current.UserName,
            Email = request.Email ?? current.Email,
            Password = string.IsNullOrWhiteSpace(request.Password) ? current.Password : request.Password,
            UserType = request.UserType.HasValue ? (UserType)request.UserType.Value : current.UserType,
            ProfileImageUrl = request.ProfileImageUrl ?? current.ProfileImageUrl,
            Street = request.Street ?? current.Street,
            Number = request.Number ?? current.Number,
            Complement = request.Complement ?? current.Complement,
            ZipCode = request.ZipCode ?? current.ZipCode,
            City = request.City ?? current.City,
            State = request.State ?? current.State,
            Phone = request.Phone ?? current.Phone,
            Credits = current.Credits
        };

        var updated = await _userService.UpdateAsync(userId, merged);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user is null) return NotFound();
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create(User request)
    {
        var created = await _userService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<User>> Update(Guid id, User request)
    {
        var updated = await _userService.UpdateAsync(id, request);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { message = "Token inválido." });

        var removed = await _userService.DeleteAsync(userId);
        if (!removed) return NotFound();
        return NoContent();
    }
}


