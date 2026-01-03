using FreelaverseApi.Data;
using FreelaverseApi.Models;
using Freelaverse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Freelaverse.Data.Services;

public class ProfessionalAreaService : IProfessionalAreaService
{
    private readonly AppDbContext _context;

    public ProfessionalAreaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProfessionalAreas>> GetAllAsync()
    {
        return await _context.ProfessionalArea.AsNoTracking().ToListAsync();
    }

    public async Task<ProfessionalAreas?> GetByIdAsync(Guid id)
    {
        return await _context.ProfessionalArea.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ProfessionalAreas> CreateAsync(ProfessionalAreas area)
    {
        area.Id = Guid.NewGuid();
        _context.ProfessionalArea.Add(area);
        await _context.SaveChangesAsync();
        return area;
    }

    public async Task<ProfessionalAreas?> UpdateAsync(Guid id, ProfessionalAreas area)
    {
        var existing = await _context.ProfessionalArea.FindAsync(id);
        if (existing is null) return null;

        existing.Name = area.Name;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.ProfessionalArea.FindAsync(id);
        if (entity is null) return false;

        _context.ProfessionalArea.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}


