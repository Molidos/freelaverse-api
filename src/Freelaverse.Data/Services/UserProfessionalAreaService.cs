using FreelaverseApi.Data;
using FreelaverseApi.Models;
using Freelaverse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Freelaverse.Data.Services;

public class UserProfessionalAreaService : IUserProfessionalAreaService
{
    private readonly AppDbContext _context;

    public UserProfessionalAreaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<UserProfessionalAreas>> GetAllAsync()
    {
        return await _context.UserProfessionalArea
            .Include(upa => upa.User)
            .Include(upa => upa.ProfessionalArea)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<UserProfessionalAreas?> GetByIdAsync(Guid id)
    {
        return await _context.UserProfessionalArea
            .Include(upa => upa.User)
            .Include(upa => upa.ProfessionalArea)
            .AsNoTracking()
            .FirstOrDefaultAsync(upa => upa.Id == id);
    }

    public async Task<UserProfessionalAreas> CreateAsync(UserProfessionalAreas relation)
    {
        // evitar que navegações instanciadas gerem inserts indesejados
        relation.User = null;
        relation.ProfessionalArea = null;

        _context.UserProfessionalArea.Add(relation);
        await _context.SaveChangesAsync();
        return relation;
    }

    public async Task<UserProfessionalAreas?> UpdateAsync(Guid id, UserProfessionalAreas relation)
    {
        var existing = await _context.UserProfessionalArea.FindAsync(id);
        if (existing is null) return null;

        existing.UserId = relation.UserId;
        existing.ProfessionalAreaId = relation.ProfessionalAreaId;

        existing.User = null;
        existing.ProfessionalArea = null;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.UserProfessionalArea.FindAsync(id);
        if (entity is null) return false;

        _context.UserProfessionalArea.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}


