using FreelaverseApi.Models;

namespace Freelaverse.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailAndPasswordAsync(string email, string password);
    Task<User> CreateAsync(User user);
    Task<User?> UpdateAsync(Guid id, User user);
    Task<bool> DeleteAsync(Guid id);
}


