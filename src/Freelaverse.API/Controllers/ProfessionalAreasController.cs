using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Freelaverse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfessionalAreasController : ControllerBase
{
    private readonly IProfessionalAreaService _service;

    public ProfessionalAreasController(IProfessionalAreaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProfessionalAreas>>> GetAll()
    {
        var areas = await _service.GetAllAsync();
        return Ok(areas);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProfessionalAreas>> GetById(Guid id)
    {
        var area = await _service.GetByIdAsync(id);
        if (area is null) return NotFound();
        return Ok(area);
    }

    [HttpPost]
    public async Task<ActionResult<ProfessionalAreas>> Create(ProfessionalAreas request)
    {
        var created = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProfessionalAreas>> Update(Guid id, ProfessionalAreas request)
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


