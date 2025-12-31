using FreelaverseApi.Data;
using FreelaverseApi.Models;
using Freelaverse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Freelaverse.Data.Services;

public class ServiceService : IServiceService
{
    private readonly AppDbContext _context;

    public ServiceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Service>> GetAllAsync()
    {
        return await _context.Services
            .Include(s => s.ProfessionalService)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Service?> GetByIdAsync(Guid id)
    {
        return await _context.Services
            .Include(s => s.ProfessionalService)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Service> CreateAsync(Service service)
    {
        _context.Services.Add(service);
        await _context.SaveChangesAsync();
        return service;
    }

    public async Task<Service?> UpdateAsync(Guid id, Service service)
    {
        var existing = await _context.Services.FindAsync(id);
        if (existing is null) return null;

        existing.Title = service.Title;
        existing.Description = service.Description;
        existing.Category = service.Category;
        existing.Urgency = service.Urgency;
        existing.Status = service.Status;
        existing.Address = service.Address;
        existing.UserId = service.UserId;
        existing.ClientId = service.ClientId;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.Services.FindAsync(id);
        if (entity is null) return false;

        _context.Services.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}


