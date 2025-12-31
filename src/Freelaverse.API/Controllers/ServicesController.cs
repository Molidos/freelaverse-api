using Freelaverse.Services.Interfaces;
using FreelaverseApi.Models;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<Service>> Create(Service request)
    {
        var created = await _serviceService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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


