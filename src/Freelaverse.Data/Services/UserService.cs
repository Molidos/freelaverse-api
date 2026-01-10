using FreelaverseApi.Data;
using FreelaverseApi.Models;
using Freelaverse.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace Freelaverse.Data.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.UserProfessionalArea)
                .ThenInclude(upa => upa.ProfessionalArea)
            .Include(u => u.ClientServices)
                .ThenInclude(s => s.ProfessionalService)
            .Include(u => u.ProfessionalService)
                .ThenInclude(ps => ps.Service)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.UserProfessionalArea)
                .ThenInclude(upa => upa.ProfessionalArea)
            .Include(u => u.ClientServices)
                .ThenInclude(s => s.ProfessionalService)
            .Include(u => u.ProfessionalService)
                .ThenInclude(ps => ps.Service)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByEmailAndPasswordAsync(string email, string password)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user is null) return null;

        return BCrypt.Net.BCrypt.Verify(password, user.Password) ? user : null;
    }

    public async Task<User> CreateAsync(User user)
    {
        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> UpdateAsync(Guid id, User user)
    {
        var existing = await _context.Users.FindAsync(id);
        if (existing is null) return null;

        // Campos simples
        existing.UserName = user.UserName;
        existing.Email = user.Email;
        existing.Password = user.Password;
        existing.UserType = user.UserType;
        existing.ProfileImageUrl = user.ProfileImageUrl;

        // Endereço
        existing.Street = user.Street;
        existing.Number = user.Number;
        existing.Complement = user.Complement;
        existing.ZipCode = user.ZipCode;
        existing.City = user.City;
        existing.State = user.State;

        // Contato
        existing.Phone = user.Phone;

        // Senha (se enviada)
        if (!string.IsNullOrWhiteSpace(user.Password))
        {
            existing.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<User?> AddCreditsAsync(Guid id, int creditsToAdd)
    {
        var existing = await _context.Users.FindAsync(id);
        if (existing is null) return null;

        existing.Credits += creditsToAdd;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _context.Users.FindAsync(id);
        if (entity is null) return false;

        _context.Users.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}


