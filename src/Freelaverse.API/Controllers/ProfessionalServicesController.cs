using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfessionalServicesController : ControllerBase
{
    private readonly IProfessionalServiceService _service;

    public ProfessionalServicesController(IProfessionalServiceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProfessionalService>>> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProfessionalService>> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ProfessionalService>> Create(ProfessionalService request)
    {
        var created = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProfessionalService>> Update(Guid id, ProfessionalService request)
    {
        var updated = await _service.UpdateAsync(id, request);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var removed = await _service.DeleteAsync(id);
        if (!removed) return NotFound();
        return NoContent();
    }
}


