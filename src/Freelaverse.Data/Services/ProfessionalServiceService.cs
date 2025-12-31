using FreelaverseApi.Data;
using FreelaverseApi.Models;
using Freelaverse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Freelaverse.Data.Services;

public class ProfessionalServiceService : IProfessionalServiceService
{
    private readonly AppDbContext _context;

    public ProfessionalServiceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProfessionalService>> GetAllAsync()
    {
        return await _context.ProfessionalServices
            .Include(ps => ps.Professional)
            .Include(ps => ps.Service)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ProfessionalService?> GetByIdAsync(Guid id)
    {
        return await _context.ProfessionalServices
            .Include(ps => ps.Professional)
            .Include(ps => ps.Service)
            .AsNoTracking()
            .FirstOrDefaultAsync(ps => ps.Id == id);
    }

    public async Task<ProfessionalService> CreateAsync(ProfessionalService professionalService)
    {
        _context.ProfessionalServices.Add(professionalService);
        await _context.SaveChangesAsync();
        return professionalService;
    }

    public async Task<ProfessionalService?> UpdateAsync(Guid id, ProfessionalService professionalService)
    {
        var existing = await _context.ProfessionalServices.FindAsync(id);
        if (existing is null) return null;

        existing.ProfessionalId = professionalService.ProfessionalId;
        existing.ServiceId = professionalService.ServiceId;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.ProfessionalServices.FindAsync(id);
        if (entity is null) return false;

        _context.ProfessionalServices.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}


