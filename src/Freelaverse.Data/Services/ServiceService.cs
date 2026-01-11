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

    public async Task<Service?> GetByIdWithClientAsync(Guid id)
    {
        return await _context.Services
            .Include(s => s.Client)
            .Include(s => s.ProfessionalService)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Service> CreateAsync(Service service)
    {
        if (service.Value <= 0) service.Value = 150;
        service.QuantProfessionals = 0;

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
        if (service.Value > 0) existing.Value = service.Value;
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

    public async Task<(IEnumerable<Service> Items, int TotalCount)> GetByCategoryPagedAsync(IEnumerable<string>? categories, int page, int pageSize, Guid? excludeProfessionalId = null)
    {
        var query = _context.Services
            .AsNoTracking()
            .Include(s => s.ProfessionalService)
            .Where(s => s.Status.ToLower() == "pendente")
            .Where(s => s.CreatedAt >= DateTimeOffset.UtcNow.AddMonths(-1));

        if (categories != null && categories.Any())
        {
            var catSet = categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.ToLower())
                .ToHashSet();

            if (catSet.Count > 0)
            {
                query = query.Where(s => catSet.Contains(s.Category.ToLower()));
            }
        }

        if (excludeProfessionalId.HasValue)
        {
            var pid = excludeProfessionalId.Value;
            query = query.Where(s => !s.ProfessionalService.Any(ps => ps.ProfessionalId == pid));
        }

        query = query.OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}


