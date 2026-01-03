using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;

    public ServicesController(IServiceService serviceService)
    {
        _serviceService = serviceService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Service>>> GetAll()
    {
        var services = await _serviceService.GetAllAsync();
        return Ok(services);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Service>> GetById(Guid id)
    {
        var service = await _serviceService.GetByIdAsync(id);
        if (service is null) return NotFound();
        return Ok(service);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Service>> Create(Service request)
    {
        // força o usuário autenticado como dono do serviço
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized();

        request.UserId = userId;
        request.ClientId = userId;

        var created = await _serviceService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("search")]
    public async Task<ActionResult<object>> SearchByCategory([FromQuery] List<string>? categories, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1) return BadRequest("page e pageSize devem ser maiores que zero.");

        var (items, total) = await _serviceService.GetByCategoryPagedAsync(categories, page, pageSize);
        var totalPages = (int)Math.Ceiling((double)total / pageSize);

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages,
            items
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Service>> Update(Guid id, Service request)
    {
        var updated = await _serviceService.UpdateAsync(id, request);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var removed = await _serviceService.DeleteAsync(id);
        if (!removed) return NotFound();
        return NoContent();
    }
}


